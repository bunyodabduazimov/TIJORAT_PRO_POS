using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class PaymentWindow : Window
{
    private const string CashType = "Наличные";
    private const string CardType = "Карта";
    private const string MixedType = "Смешанная";

    private readonly DatabaseService _databaseService = new();
    private readonly decimal _subtotal;
    private readonly List<Cash> _cashes = new();
    private bool _isRefreshing;
    private string _activeInput = "cash";
    private People _selectedCustomer = new()
    {
        Id = 1,
        Name = "Розничный покупатель",
        Status = 1
    };

    public PaymentWindow(decimal total, string paymentType, decimal discount = 0m)
    {
        InitializeComponent();

        _subtotal = total;
        PaymentType = NormalizePaymentType(paymentType);
        DiscountAmount = Math.Clamp(discount, 0m, _subtotal);

        LoadCashes();
        SetupInitialAmounts();
        SetupQuickAmounts();
        RefreshUi(updateDiscountFields: true);
    }

    public string PaymentType { get; private set; }
    public decimal ReceivedAmount { get; private set; }
    public decimal CashAmount { get; private set; }
    public decimal CardAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public int PeopleId => _selectedCustomer.Id;
    public bool HoldRequested { get; private set; }

    private decimal TotalDue => Math.Max(0m, _subtotal - DiscountAmount);

    private void LoadCashes()
    {
        try
        {
            _databaseService.InitializeAsync().GetAwaiter().GetResult();
            _cashes.AddRange(_databaseService.GetCashesAsync().GetAwaiter().GetResult());
        }
        catch
        {
            _cashes.Clear();
        }

        if (_cashes.Count == 0)
        {
            _cashes.Add(new Cash { Id = 1, Name = "Касса Бунёд", Status = 1 });
            _cashes.Add(new Cash { Id = 2, Name = "Карта", Status = 1 });
        }

        CashAccountBox.ItemsSource = _cashes;
        CardAccountBox.ItemsSource = _cashes;
        CashAccountBox.SelectedIndex = 0;
        CardAccountBox.SelectedIndex = _cashes.Count > 1 ? 1 : 0;
    }

    private void SetupInitialAmounts()
    {
        TotalBox.Text = FormatMoney(_subtotal);
        PayableBox.Text = FormatMoney(TotalDue);
        CustomerText.Text = _selectedCustomer.Name ?? "Розничный покупатель";

        if (PaymentType == CardType)
        {
            SecondPaymentRow.Visibility = Visibility.Visible;
            CardPaymentBox.Text = FormatInput(TotalDue);
            _activeInput = "card";
            return;
        }

        if (PaymentType == MixedType)
        {
            SecondPaymentRow.Visibility = Visibility.Visible;
            CashPaymentBox.Text = FormatInput(TotalDue);
            _activeInput = "cash";
            return;
        }

        SecondPaymentRow.Visibility = Visibility.Collapsed;
        CashPaymentBox.Text = FormatInput(TotalDue);
        _activeInput = "cash";
    }

    private void DiscountPercentFocused(object sender, RoutedEventArgs e)
    {
        _activeInput = "discountPercent";
        SelectText(DiscountPercentBox);
    }

    private void DiscountAmountFocused(object sender, RoutedEventArgs e)
    {
        _activeInput = "discountAmount";
        SelectText(DiscountAmountBox);
    }

    private void CashPaymentFocused(object sender, RoutedEventArgs e)
    {
        _activeInput = "cash";
        SelectText(CashPaymentBox);
    }

    private void CardPaymentFocused(object sender, RoutedEventArgs e)
    {
        _activeInput = "card";
        SelectText(CardPaymentBox);
    }

    private void DiscountPercentChanged(object sender, TextChangedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        var percent = Math.Clamp(ParseAmount(DiscountPercentBox.Text), 0m, 100m);
        DiscountAmount = Math.Round(_subtotal * percent / 100m, 2);
        RefreshUi(updateDiscountAmount: true);
        RefillSinglePaymentRemainder();
    }

    private void DiscountAmountChanged(object sender, TextChangedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        DiscountAmount = Math.Clamp(ParseAmount(DiscountAmountBox.Text), 0m, _subtotal);
        RefreshUi(updateDiscountPercent: true);
        RefillSinglePaymentRemainder();
    }

    private void PaymentAmountChanged(object sender, TextChangedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        RefreshUi();
    }

    private void CustomerClicked(object sender, RoutedEventArgs e)
    {
        var window = new CustomerSelectWindow(_selectedCustomer.Id)
        {
            Owner = this
        };

        if (window.ShowDialog() != true || window.SelectedCustomer is null)
        {
            return;
        }

        _selectedCustomer = window.SelectedCustomer;
        CustomerText.Text = string.IsNullOrWhiteSpace(_selectedCustomer.Name)
            ? $"Клиент №{_selectedCustomer.Id}"
            : _selectedCustomer.Name;
    }

    private void NumberClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value })
        {
            return;
        }

        var box = GetActiveTextBox();
        if (box is null)
        {
            return;
        }

        var input = box.Text.Trim();
        if (value == "." && input.Contains('.'))
        {
            return;
        }

        if (input == "0" && value != ".")
        {
            input = string.Empty;
        }

        if (input.Length < 10)
        {
            box.Text = input + value;
            box.CaretIndex = box.Text.Length;
        }
    }

    private void BackspaceClicked(object sender, RoutedEventArgs e)
    {
        var box = GetActiveTextBox();
        if (box is null)
        {
            return;
        }

        box.Text = box.Text.Length > 0 ? box.Text[..^1] : string.Empty;
        box.CaretIndex = box.Text.Length;
    }

    private void ClearClicked(object sender, RoutedEventArgs e)
    {
        var box = GetActiveTextBox();
        if (box is null)
        {
            return;
        }

        box.Text = string.Empty;
        box.Focus();
    }

    private void ExactAmountClicked(object sender, RoutedEventArgs e)
    {
        if (_activeInput is "discountPercent" or "discountAmount")
        {
            _activeInput = "cash";
        }

        var paid = ParseAmount(CashPaymentBox.Text) +
            (SecondPaymentRow.Visibility == Visibility.Visible ? ParseAmount(CardPaymentBox.Text) : 0m);
        var remainder = Math.Max(0m, TotalDue - paid);
        var box = GetActiveTextBox();
        if (box is null)
        {
            return;
        }

        box.Text = FormatInput(ParseAmount(box.Text) + remainder);
        box.CaretIndex = box.Text.Length;
    }

    private void QuickAmountClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: decimal amount })
        {
            return;
        }

        if (_activeInput is "discountPercent" or "discountAmount")
        {
            _activeInput = "cash";
        }

        var box = GetActiveTextBox();
        if (box is null)
        {
            return;
        }

        box.Text = FormatInput(amount);
        box.CaretIndex = box.Text.Length;
    }

    private void ClearCashClicked(object sender, RoutedEventArgs e)
    {
        CashPaymentBox.Text = string.Empty;
        _activeInput = "cash";
        CashPaymentBox.Focus();
    }

    private void ClearCardClicked(object sender, RoutedEventArgs e)
    {
        CardPaymentBox.Text = string.Empty;
        _activeInput = "card";
        CardPaymentBox.Focus();
    }

    private void AddPaymentClicked(object sender, RoutedEventArgs e)
    {
        SecondPaymentRow.Visibility = Visibility.Visible;
        PaymentType = MixedType;

        var paid = ParseAmount(CashPaymentBox.Text) + ParseAmount(CardPaymentBox.Text);
        if (paid < TotalDue && string.IsNullOrWhiteSpace(CardPaymentBox.Text))
        {
            CardPaymentBox.Text = FormatInput(TotalDue - paid);
        }

        _activeInput = "card";
        CardPaymentBox.Focus();
        RefreshUi();
    }

    private void HoldClicked(object sender, RoutedEventArgs e)
    {
        HoldRequested = true;
        DialogResult = false;
        Close();
    }

    private void ConfirmClicked(object sender, RoutedEventArgs e)
    {
        CashAmount = ParseAmount(CashPaymentBox.Text);
        CardAmount = SecondPaymentRow.Visibility == Visibility.Visible ? ParseAmount(CardPaymentBox.Text) : 0m;
        ReceivedAmount = CashAmount + CardAmount;

        if (ReceivedAmount < TotalDue)
        {
            RemainderLabel.Foreground = BrushFrom("#FF2B22");
            RemainderText.Foreground = BrushFrom("#FF2B22");
            return;
        }

        PaymentType = ResolvePaymentType();
        DialogResult = true;
        Close();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void RefreshUi(
        bool updateDiscountFields = false,
        bool updateDiscountPercent = false,
        bool updateDiscountAmount = false)
    {
        _isRefreshing = true;
        try
        {
            CashAmount = ParseAmount(CashPaymentBox.Text);
            CardAmount = SecondPaymentRow.Visibility == Visibility.Visible ? ParseAmount(CardPaymentBox.Text) : 0m;
            ReceivedAmount = CashAmount + CardAmount;

            if (updateDiscountFields || updateDiscountPercent)
            {
                var percent = _subtotal <= 0 ? 0 : DiscountAmount / _subtotal * 100m;
                DiscountPercentBox.Text = FormatInput(Math.Round(percent, 2));
            }

            if (updateDiscountFields || updateDiscountAmount)
            {
                DiscountAmountBox.Text = FormatInput(DiscountAmount);
            }

            TotalBox.Text = FormatMoney(_subtotal);
            PayableBox.Text = FormatMoney(TotalDue);
            ReceivedText.Text = $"{ReceivedAmount:0.00} TJS";
            RemainderText.Text = $"{Math.Max(0m, TotalDue - ReceivedAmount):0.00} TJS";
            KeyboardTitle.Text = GetKeyboardTitle();
            ConfirmButtonText.Text = $"Оплатить {Math.Min(ReceivedAmount, TotalDue):0.##} c";

            RemainderLabel.Foreground = BrushFrom("#344054");
            RemainderText.Foreground = ReceivedAmount >= TotalDue ? BrushFrom("#23A455") : BrushFrom("#FF2B22");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void SetupQuickAmounts()
    {
        SetQuickButton(QuickAmount1Button, RoundUp(TotalDue, 10m));
        SetQuickButton(QuickAmount2Button, RoundUp(TotalDue, 50m));
        SetQuickButton(QuickAmount3Button, RoundUp(TotalDue, 100m));
    }

    private void RefillSinglePaymentRemainder()
    {
        if (SecondPaymentRow.Visibility != Visibility.Visible)
        {
            CashPaymentBox.Text = FormatInput(TotalDue);
        }
    }

    private TextBox? GetActiveTextBox()
    {
        return _activeInput switch
        {
            "discountPercent" => DiscountPercentBox,
            "discountAmount" => DiscountAmountBox,
            "card" => SecondPaymentRow.Visibility == Visibility.Visible ? CardPaymentBox : CashPaymentBox,
            _ => CashPaymentBox
        };
    }

    private string GetKeyboardTitle()
    {
        return _activeInput switch
        {
            "discountPercent" => "Введите процент скидки",
            "discountAmount" => "Введите сумму скидки",
            "card" => "Введите сумму второй оплаты",
            _ => "Введите сумму"
        };
    }

    private string ResolvePaymentType()
    {
        if (CashAmount > 0 && CardAmount > 0)
        {
            return MixedType;
        }

        return CardAmount > 0 ? CardType : CashType;
    }

    private static string NormalizePaymentType(string paymentType)
    {
        return paymentType switch
        {
            CardType => CardType,
            MixedType => MixedType,
            _ => CashType
        };
    }

    private static void SetQuickButton(Button button, decimal amount)
    {
        button.Content = amount.ToString("0.##", CultureInfo.InvariantCulture);
        button.Tag = amount;
    }

    private static decimal RoundUp(decimal value, decimal step)
    {
        if (step <= 0)
        {
            return value;
        }

        return Math.Ceiling(value / step) * step;
    }

    private static decimal ParseAmount(string? value)
    {
        return decimal.TryParse(value?.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 0m;
    }

    private static string FormatInput(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static void SelectText(TextBox box)
    {
        box.Dispatcher.BeginInvoke(() =>
        {
            box.SelectAll();
            box.CaretIndex = box.Text.Length;
        });
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
