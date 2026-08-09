using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FFPOS.Views;

public partial class PaymentWindow : Window
{
    private const string CashType = "Наличные";
    private const string CardType = "Карта";
    private const string MixedType = "Смешанная";

    private readonly decimal _total;
    private string _activeAmount = "cash";
    private string _cashInput = string.Empty;
    private string _cardInput = string.Empty;

    public PaymentWindow(decimal total, string paymentType)
    {
        InitializeComponent();

        _total = total;
        PaymentType = NormalizePaymentType(paymentType);

        if (PaymentType == CardType)
        {
            _cardInput = FormatInput(total);
            _activeAmount = "card";
        }
        else
        {
            _cashInput = FormatInput(total);
            _activeAmount = "cash";
        }

        TotalText.Text = $"{_total:0} c";
        RefreshUi();
    }

    public string PaymentType { get; private set; }

    public decimal ReceivedAmount { get; private set; }

    public decimal CashAmount { get; private set; }

    public decimal CardAmount { get; private set; }

    private void PaymentTypeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Content is not string type)
        {
            return;
        }

        PaymentType = NormalizePaymentType(type);

        if (PaymentType == CashType)
        {
            _activeAmount = "cash";
            _cashInput = FormatInput(_total);
            _cardInput = string.Empty;
        }
        else if (PaymentType == CardType)
        {
            _activeAmount = "card";
            _cashInput = string.Empty;
            _cardInput = FormatInput(_total);
        }
        else
        {
            _activeAmount = "cash";
            _cashInput = string.Empty;
            _cardInput = string.Empty;
        }

        RefreshUi();
    }

    private void CashAmountClicked(object sender, RoutedEventArgs e)
    {
        _activeAmount = "cash";
        if (PaymentType != MixedType)
        {
            PaymentType = CashType;
            _cardInput = string.Empty;
        }

        RefreshUi();
    }

    private void CardAmountClicked(object sender, RoutedEventArgs e)
    {
        _activeAmount = "card";
        if (PaymentType != MixedType)
        {
            PaymentType = CardType;
            _cashInput = string.Empty;
        }

        RefreshUi();
    }

    private void NumberClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string digit })
        {
            return;
        }

        var input = GetActiveInput();
        if (input == "0")
        {
            input = string.Empty;
        }

        if (input.Length < 8)
        {
            SetActiveInput(input + digit);
            RefreshUi();
        }
    }

    private void BackspaceClicked(object sender, RoutedEventArgs e)
    {
        var input = GetActiveInput();
        SetActiveInput(input.Length > 0 ? input[..^1] : string.Empty);
        RefreshUi();
    }

    private void ClearClicked(object sender, RoutedEventArgs e)
    {
        SetActiveInput(string.Empty);
        RefreshUi();
    }

    private void ExactAmountClicked(object sender, RoutedEventArgs e)
    {
        if (PaymentType == MixedType)
        {
            var current = ParseAmount(_cashInput) + ParseAmount(_cardInput);
            var remainder = Math.Max(0m, _total - current);
            SetActiveInput(FormatInput(ParseAmount(GetActiveInput()) + remainder));
        }
        else
        {
            SetActiveInput(FormatInput(_total));
        }

        RefreshUi();
    }

    private void ConfirmClicked(object sender, RoutedEventArgs e)
    {
        CashAmount = ParseAmount(_cashInput);
        CardAmount = ParseAmount(_cardInput);
        ReceivedAmount = CashAmount + CardAmount;

        if (ReceivedAmount < _total)
        {
            RemainderText.Foreground = new SolidColorBrush(Color.FromRgb(255, 43, 34));
            return;
        }

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

    private void RefreshUi()
    {
        CashAmount = ParseAmount(_cashInput);
        CardAmount = ParseAmount(_cardInput);
        ReceivedAmount = CashAmount + CardAmount;

        CashAmountText.Text = $"{CashAmount:0} c";
        CardAmountText.Text = $"{CardAmount:0} c";
        RemainderText.Text = $"{Math.Max(0m, _total - ReceivedAmount):0} c";
        ChangeText.Text = $"{Math.Max(0m, ReceivedAmount - _total):0} c";
        KeyboardTitle.Text = _activeAmount == "cash" ? "Введите наличные" : "Введите сумму карты";
        ConfirmButton.Content = $"Оплатить {Math.Min(ReceivedAmount, _total):0} c";

        ApplyPaymentTypeState(CashButton, PaymentType == CashType);
        ApplyPaymentTypeState(CardButton, PaymentType == CardType);
        ApplyPaymentTypeState(MixedButton, PaymentType == MixedType);
        ApplyAmountState(CashAmountPanel, _activeAmount == "cash");
        ApplyAmountState(CardAmountPanel, _activeAmount == "card");

        RemainderText.Foreground = Brushes.White;
        CashAmountButton.IsEnabled = PaymentType != CardType;
        CardAmountButton.IsEnabled = PaymentType != CashType;
    }

    private string GetActiveInput()
    {
        return _activeAmount == "cash" ? _cashInput : _cardInput;
    }

    private void SetActiveInput(string value)
    {
        if (_activeAmount == "cash")
        {
            _cashInput = value;
        }
        else
        {
            _cardInput = value;
        }
    }

    private static decimal ParseAmount(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 0m;
    }

    private static string FormatInput(decimal value)
    {
        return ((int)Math.Ceiling(value)).ToString(CultureInfo.InvariantCulture);
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

    private static void ApplyPaymentTypeState(Button button, bool isSelected)
    {
        button.Background = isSelected ? BrushFrom("#FF2B22") : Brushes.White;
        button.Foreground = isSelected ? Brushes.White : BrushFrom("#102033");
        button.BorderBrush = isSelected ? BrushFrom("#FF2B22") : BrushFrom("#DEE4EC");
    }

    private static void ApplyAmountState(Border panel, bool isSelected)
    {
        panel.Background = isSelected ? BrushFrom("#FFF0EF") : Brushes.Transparent;
        panel.BorderBrush = isSelected ? BrushFrom("#FF2B22") : Brushes.Transparent;
        panel.BorderThickness = isSelected ? new Thickness(1) : new Thickness(0);
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
