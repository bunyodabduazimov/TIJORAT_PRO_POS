using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class DdsOperationWindow : Window
{
    private const double DescriptionLineHeight = 22;
    private const double DescriptionVerticalPadding = 10;

    private readonly DatabaseService _databaseService = new();
    private readonly string _operationType;
    private People _selectedCustomer = new()
    {
        Id = 1,
        Name = "\u0420\u043e\u0437\u043d\u0438\u0447\u043d\u044b\u0439 \u043f\u043e\u043a\u0443\u043f\u0430\u0442\u0435\u043b\u044c",
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
        UpdateCustomerInfo();
        UpdateDirectionInfo();
        LoadCashes();
        LoadArticles();
        UpdateDescriptionHeight();
    }

    public Dds? Operation { get; private set; }

    private void LoadArticles()
    {
        try
        {
            var allArticles = _databaseService.GetArticlesAsync().GetAwaiter().GetResult()
                .Where(article => !string.IsNullOrWhiteSpace(article.Name))
                .ToList();

            var filteredArticles = allArticles
                .Where(article => IsArticleTypeMatch(article.Type))
                .ToList();
            var articles = filteredArticles.Count > 0 ? filteredArticles : allArticles;
            if (articles.Count == 0)
            {
                articles = new List<Article>
                {
                    new() { Id = 1, Name = "\u0421\u0442\u0430\u0442\u044c\u044f \u21161", Status = 1 }
                };
            }

            ArticleBox.ItemsSource = articles;
            ArticleBox.SelectedIndex = -1;
        }
        catch
        {
            var articles = new[]
            {
                new Article { Id = 1, Name = "\u0421\u0442\u0430\u0442\u044c\u044f \u21161", Status = 1 }
            };
            ArticleBox.ItemsSource = articles;
            ArticleBox.SelectedIndex = -1;
        }
    }

    private bool IsArticleTypeMatch(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return true;
        }

        var normalized = type.Trim().ToLowerInvariant();
        var isIncome = DdsOperationTypes.IsIncome(_operationType);
        return isIncome
            ? normalized is "income" or "in" or "plus" or "1" or "\u043f\u0440\u0438\u0445\u043e\u0434"
            : normalized is "expense" or "out" or "minus" or "2" or "\u0440\u0430\u0441\u0445\u043e\u0434";
    }

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
            CashBox.ItemsSource = new[] { new Cash { Id = 1, Name = "\u041a\u0430\u0441\u0441\u0430", Status = 1 } };
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
            UpdateCustomerInfo();
        }
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0)
        {
            ErrorText.Text = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u0441\u0443\u043c\u043c\u0443";
            AmountBox.Focus();
            return;
        }

        if (ArticleBox.SelectedItem is not Article article)
        {
            ErrorText.Text = "\u0412\u044B\u0431\u0435\u0440\u0438\u0442\u0435 \u0441\u0442\u0430\u0442\u044C\u044E";
            ArticleBox.Focus();
            ArticleBox.IsDropDownOpen = true;
            return;
        }

        var now = DateTime.Now;
        Operation = new Dds
        {
            StoreId = new AppSettingsService().Load().StoreId,
            UserId = App.CurrentUser?.Id ?? 1,
            CashId = CashBox.SelectedItem is Cash cash ? cash.Id : 1,
            PeopleId = DdsOperationTypes.RequiresCustomer(_operationType) ? _selectedCustomer.Id : 1,
            ArticleId = article.Id,
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

    private void AmountKeypadClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Content: string value })
        {
            return;
        }

        var text = AmountBox.Text.Trim();
        if (value == "." && (text.Contains('.') || text.Contains(',')))
        {
            AmountBox.Focus();
            AmountBox.CaretIndex = AmountBox.Text.Length;
            return;
        }

        if (text == "0" && value != ".")
        {
            AmountBox.Text = value;
        }
        else
        {
            AmountBox.Text += value;
        }

        AmountBox.Focus();
        AmountBox.CaretIndex = AmountBox.Text.Length;
    }

    private void AmountBackspaceClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AmountBox.Text))
        {
            AmountBox.Focus();
            return;
        }

        AmountBox.Text = AmountBox.Text[..^1];
        AmountBox.Focus();
        AmountBox.CaretIndex = AmountBox.Text.Length;
    }

    private void DescriptionBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateDescriptionHeight();
    }

    private void UpdateCustomerInfo()
    {
        CustomerText.Text = _selectedCustomer.Name ?? $"\u041a\u043b\u0438\u0435\u043d\u0442 \u2116{_selectedCustomer.Id}";
        CustomerBalanceText.Text = $"\u0411\u0430\u043b\u0430\u043d\u0441: {_selectedCustomer.Balance:0.00} c";
    }

    private void UpdateDirectionInfo()
    {
        var isIncome = DdsOperationTypes.IsIncome(_operationType);
        DirectionText.Text = isIncome ? "\u041f\u0440\u0438\u0445\u043e\u0434" : "\u0420\u0430\u0441\u0445\u043e\u0434";
        DirectionText.Foreground = System.Windows.Media.Brushes.White;
        DirectionBadge.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(isIncome ? "#16A34A" : "#FF2B22")!;
    }

    private void UpdateDescriptionHeight()
    {
        if (!IsInitialized)
        {
            return;
        }

        DescriptionBox.UpdateLayout();
        var lineCount = Math.Max(1, DescriptionBox.LineCount);
        var nextHeight = Math.Min(DescriptionBox.MaxHeight, Math.Max(DescriptionBox.MinHeight, lineCount * DescriptionLineHeight + DescriptionVerticalPadding));
        DescriptionBox.Height = nextHeight;
        DescriptionBox.VerticalScrollBarVisibility = nextHeight >= DescriptionBox.MaxHeight
            ? System.Windows.Controls.ScrollBarVisibility.Visible
            : System.Windows.Controls.ScrollBarVisibility.Disabled;
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
