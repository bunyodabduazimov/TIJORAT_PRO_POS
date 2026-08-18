using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class DdsOperationWindow : Window
{
    private readonly DatabaseService _databaseService = new();
    private readonly string _operationType;
    private People _selectedCustomer = new()
    {
        Id = 1,
        Name = "Розничный покупатель",
        Status = 1
    };

    public DdsOperationWindow(string operationType)
    {
        _operationType = operationType;
        InitializeComponent();
        TitleText.Text = DdsOperationTypes.GetTitle(operationType);
        CustomerPanel.Visibility = DdsOperationTypes.RequiresCustomer(operationType)
            ? Visibility.Visible
            : Visibility.Collapsed;
        LoadCashes();
        LoadArticles();
    }

    private void LoadArticles()
    {
        try
        {
            var expectedType = DdsOperationTypes.IsIncome(_operationType) ? "income" : "expense";
            var articles = _databaseService.GetArticlesAsync().GetAwaiter().GetResult()
                .Where(article => string.IsNullOrWhiteSpace(article.Type) ||
                                  string.Equals(article.Type, expectedType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            ArticleBox.ItemsSource = articles;
            ArticleBox.SelectedItem = articles.FirstOrDefault();
        }
        catch
        {
            ArticleBox.ItemsSource = Array.Empty<Article>();
        }
    }

    public Dds? Operation { get; private set; }

    private void LoadCashes()
    {
        try
        {
            _databaseService.InitializeAsync().GetAwaiter().GetResult();
            var cashes = _databaseService.GetCashesAsync().GetAwaiter().GetResult();
            CashBox.ItemsSource = cashes;
            CashBox.SelectedItem = cashes.FirstOrDefault(cash => cash.Id == App.CurrentUser?.CashId)
                ?? cashes.FirstOrDefault();
        }
        catch
        {
            CashBox.ItemsSource = new[] { new Cash { Id = 1, Name = "Касса", Status = 1 } };
            CashBox.SelectedIndex = 0;
        }
    }

    private void SelectCustomerClicked(object sender, RoutedEventArgs e)
    {
        var window = new CustomerSelectWindow(_selectedCustomer.Id)
        {
            Owner = this
        };

        if (window.ShowDialog() == true && window.SelectedCustomer is not null)
        {
            _selectedCustomer = window.SelectedCustomer;
            CustomerText.Text = _selectedCustomer.Name ?? $"Клиент №{_selectedCustomer.Id}";
        }
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0)
        {
            ErrorText.Text = "Введите сумму";
            AmountBox.Focus();
            return;
        }

        var now = DateTime.Now;
        var articleId = ArticleBox.SelectedItem is Article article ? article.Id : 1;
        Operation = new Dds
        {
            StoreId = new AppSettingsService().Load().StoreId,
            UserId = App.CurrentUser?.Id ?? 1,
            CashId = CashBox.SelectedItem is Cash cash ? cash.Id : 1,
            PeopleId = DdsOperationTypes.RequiresCustomer(_operationType) ? _selectedCustomer.Id : 1,
            ArticleId = articleId,
            Summa = amount,
            EventTime = new DateTimeOffset(now).ToUnixTimeSeconds(),
            OrderType = _operationType,
            Description = DescriptionBox.Text.Trim(),
            Date = now.ToString("yyyy-MM-dd HH:mm:ss"),
            Status = 1,
            SyncStatus = 0
        };

        DialogResult = true;
        Close();
    }

    private void CancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
