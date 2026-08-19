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
    private readonly AppActivationSettings _settings;
    private readonly UserSettings _userSettings;
    private readonly decimal _subtotal;
    private readonly bool _allowDiscount;
    private readonly List<Cash> _cashes = new();
    private readonly List<TextBox> _extraPaymentBoxes = new();
    private readonly List<ComboBox> _extraPaymentCashBoxes = new();
    private bool _isRefreshing;
    private string _activeInput = "cash";
    private TextBox? _activeExtraPaymentBox;
    private People _selectedCustomer = new()
    {
        Id = 1,
        Name = "Розничный покупатель",
        Status = 1
    };

    public PaymentWindow(
        decimal total,
        string paymentType,
        decimal discount = 0m,
        string orderType = "В зале",
        int selectedPeopleId = 1,
        string? note = null,
        bool allowDiscount = true)
    {
        InitializeComponent();

        _settings = new AppSettingsService().Load();
        _userSettings = ParseUserSettings(App.CurrentUser?.Settings);
        _subtotal = total;
        _allowDiscount = allowDiscount;
        PaymentType = NormalizePaymentType(paymentType);
        OrderType = string.IsNullOrWhiteSpace(orderType) ? "В зале" : orderType;
        DiscountAmount = _allowDiscount ? Math.Clamp(discount, 0m, _subtotal) : 0m;
        NoteBox.Text = string.IsNullOrWhiteSpace(note) ? "-" : note;

        SetupOrderTypes();
        SetupDiscountAccess();
        LoadCashes();
        LoadSelectedCustomer(selectedPeopleId);
        SetupInitialAmounts();
        SetupQuickAmounts();
        RefreshUi(updateDiscountFields: true);
    }

    public string PaymentType { get; private set; }
    public string OrderType { get; private set; }
    public decimal ReceivedAmount { get; private set; }
    public decimal CashAmount { get; private set; }
    public decimal CardAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public bool PrintReceipt => PrintReceiptBox.IsChecked == true;
    public int PeopleId => _selectedCustomer.Id;
    public int CashId => CashAccountBox.SelectedItem is Cash cash ? cash.Id : 1;
    public string Note => NoteBox.Text.Trim() == "-" ? string.Empty : NoteBox.Text.Trim();
    public bool HoldRequested { get; private set; }
    public IReadOnlyList<PaymentLine> PaymentLines => GetPaymentLines();

    private decimal TotalDue => Math.Max(0m, _subtotal - DiscountAmount);

    private void SetupDiscountAccess()
    {
        DiscountLabel.Visibility = _allowDiscount ? Visibility.Visible : Visibility.Collapsed;
        DiscountEditor.Visibility = _allowDiscount ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetupOrderTypes()
    {
        PrintReceiptBox.IsChecked = _userSettings.CheckPrint;

        if (_settings.AppType != 3)
        {
            OrderTypeGrid.ColumnDefinitions.Clear();
            OrderTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            OrderTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            DineInButton.Visibility = Visibility.Collapsed;
            Grid.SetColumn(TakeAwayButton, 0);
            Grid.SetColumn(DeliveryButton, 1);
            OrderType = OrderType == "В зале" ? "С собой" : OrderType;
        }
        else
        {
            OrderTypeGrid.ColumnDefinitions.Clear();
            OrderTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            OrderTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            OrderTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            DineInButton.Visibility = Visibility.Visible;
            Grid.SetColumn(DineInButton, 0);
            Grid.SetColumn(TakeAwayButton, 1);
            Grid.SetColumn(DeliveryButton, 2);
        }

        RefreshOrderTypeButtons();
    }

    private void OrderTypeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string value)
        {
            OrderType = value;
            SetNoteOrderType(value);
            RefreshOrderTypeButtons();
        }
    }

    private void SetNoteOrderType(string value)
    {
        var note = NoteBox.Text.Trim();
        var details = GetNoteDetails(note);

        if (string.IsNullOrWhiteSpace(note) || note == "-" || note == "В зале" || note == "С собой" || note == "Доставка")
        {
            NoteBox.Text = value;
        }
        else if (!string.IsNullOrWhiteSpace(details))
        {
            NoteBox.Text = $"{value} {details}";
        }
        else
        {
            NoteBox.Text = value;
        }

        _activeInput = "note";
        NoteBox.Focus();
        NoteBox.CaretIndex = NoteBox.Text.Length;
    }

    private static string GetNoteDetails(string note)
    {
        foreach (var prefix in new[] { "В зале", "С собой", "Доставка" })
        {
            if (note.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                return note[prefix.Length..].TrimStart();
            }
        }

        return string.Empty;
    }

    private void RefreshOrderTypeButtons()
    {
        SetOrderTypeButtonState(DineInButton, OrderType == "В зале");
        SetOrderTypeButtonState(TakeAwayButton, OrderType == "С собой");
        SetOrderTypeButtonState(DeliveryButton, OrderType == "Доставка");
    }

    private static void SetOrderTypeButtonState(Button button, bool isSelected)
    {
        button.Opacity = isSelected ? 1 : 0.72;
        button.BorderThickness = isSelected ? new Thickness(2) : new Thickness(0);
        button.BorderBrush = Brushes.White;
    }

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
        SelectDefaultCashAccount();
    }

    private void SelectDefaultCashAccount()
    {
        if (_cashes.Count == 0)
        {
            CashAccountBox.SelectedIndex = -1;
            return;
        }

        var defaultCashId = _userSettings.DefaultCashId > 0
            ? _userSettings.DefaultCashId
            : App.CurrentUser?.CashId ?? 0;

        CashAccountBox.SelectedItem = _cashes.FirstOrDefault(cash => cash.Id == defaultCashId)
            ?? _cashes.FirstOrDefault();
    }

    private void LoadSelectedCustomer(int selectedPeopleId)
    {
        if (selectedPeopleId <= 1)
        {
            return;
        }

        try
        {
            var customer = _databaseService.GetPeoplesAsync().GetAwaiter().GetResult()
                .FirstOrDefault(people => people.Id == selectedPeopleId);

            if (customer is not null)
            {
                _selectedCustomer = customer;
            }
        }
        catch
        {
        }
    }

    private void SetupInitialAmounts()
    {
        TotalBox.Text = FormatMoney(_subtotal);
        PayableBox.Text = FormatMoney(TotalDue);
        CustomerText.Text = _selectedCustomer.Name ?? "Розничный покупатель";

        CashPaymentBox.Text = FormatInput(TotalDue);
        _activeInput = "cash";
    }

    private void DiscountPercentFocused(object sender, RoutedEventArgs e)
    {
        if (!_allowDiscount)
        {
            return;
        }

        _activeInput = "discountPercent";
        SelectText(DiscountPercentBox);
    }

    private void DiscountAmountFocused(object sender, RoutedEventArgs e)
    {
        if (!_allowDiscount)
        {
            return;
        }

        _activeInput = "discountAmount";
        SelectText(DiscountAmountBox);
    }

    private void CashPaymentFocused(object sender, RoutedEventArgs e)
    {
        _activeInput = "cash";
        SelectText(CashPaymentBox);
    }

    private void NoteFocused(object sender, RoutedEventArgs e)
    {
        _activeInput = "note";
        NoteBox.CaretIndex = NoteBox.Text.Length;
    }

    private void DiscountPercentChanged(object sender, TextChangedEventArgs e)
    {
        if (_isRefreshing || !_allowDiscount)
        {
            return;
        }

        var percent = Math.Clamp(ParseAmount(DiscountPercentBox.Text), 0m, 100m);
        DiscountAmount = Math.Round(_subtotal * percent / 100m, 2);
        RefillSinglePaymentRemainder();
        SetupQuickAmounts();
        RefreshUi(updateDiscountAmount: true);
    }

    private void DiscountAmountChanged(object sender, TextChangedEventArgs e)
    {
        if (_isRefreshing || !_allowDiscount)
        {
            return;
        }

        DiscountAmount = Math.Clamp(ParseAmount(DiscountAmountBox.Text), 0m, _subtotal);
        RefillSinglePaymentRemainder();
        SetupQuickAmounts();
        RefreshUi(updateDiscountPercent: true);
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
        if (_activeInput == "note")
        {
            AppendNoteKey(value);
            return;
        }

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

    private void AppendNoteKey(string value)
    {
        if (value == ".")
        {
            return;
        }

        var note = NoteBox.Text.Trim();
        NoteBox.Text = string.IsNullOrWhiteSpace(note) || note == "-"
            ? value
            : note is "В зале" or "С собой" or "Доставка"
                ? $"{note} {value}"
            : note + value;
        NoteBox.CaretIndex = NoteBox.Text.Length;
        NoteBox.Focus();
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
        if (_activeInput is "discountPercent" or "discountAmount" or "note")
        {
            _activeInput = "cash";
        }

        var paid = GetEnteredPaymentSum();
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

        if (_activeInput is "discountPercent" or "discountAmount" or "note")
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

    private void AddPaymentClicked(object sender, RoutedEventArgs e)
    {
        PaymentType = MixedType;

        var extraBox = AddExtraPaymentRow();
        var remainder = Math.Max(0m, TotalDue - GetEnteredPaymentSum());
        extraBox.Text = remainder > 0 ? FormatInput(remainder) : string.Empty;
        _activeExtraPaymentBox = extraBox;
        _activeInput = "extra";
        extraBox.Focus();
        PaymentRowsScrollViewer.ScrollToEnd();
        RefreshUi();
    }

    private TextBox AddExtraPaymentRow()
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

        var removeButton = new Button
        {
            Height = 42,
            Margin = new Thickness(0, 0, 5, 5),
            BorderBrush = BrushFrom("#FF2B22"),
            Foreground = BrushFrom("#FF2B22"),
            Style = (Style)FindResource("BaseButtonStyle"),
            Content = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = MaterialDesignThemes.Wpf.PackIconKind.DeleteOutline,
                Width = 24,
                Height = 24
            }
        };

        var comboBox = new ComboBox
        {
            Style = (Style)FindResource("PaymentComboStyle"),
            ItemsSource = _cashes,
            ItemTemplate = CashAccountBox.ItemTemplate,
            SelectedIndex = _cashes.Count > 0 ? Math.Min(_extraPaymentBoxes.Count + 2, _cashes.Count - 1) : -1
        };

        var amountBox = new TextBox
        {
            Style = (Style)FindResource("PaymentInputStyle")
        };
        amountBox.GotFocus += ExtraPaymentFocused;
        amountBox.TextChanged += PaymentAmountChanged;

        removeButton.Click += (_, _) =>
        {
            _extraPaymentBoxes.Remove(amountBox);
            _extraPaymentCashBoxes.Remove(comboBox);
            PaymentRowsPanel.Children.Remove(row);
            if (_activeExtraPaymentBox == amountBox)
            {
                _activeExtraPaymentBox = null;
                _activeInput = "cash";
            }

            RefreshUi();
        };

        Grid.SetColumn(comboBox, 1);
        Grid.SetColumn(amountBox, 3);
        row.Children.Add(removeButton);
        row.Children.Add(comboBox);
        row.Children.Add(amountBox);
        PaymentRowsPanel.Children.Add(row);
        _extraPaymentBoxes.Add(amountBox);
        _extraPaymentCashBoxes.Add(comboBox);
        return amountBox;
    }

    private void ExtraPaymentFocused(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _activeExtraPaymentBox = textBox;
            _activeInput = "extra";
            SelectText(textBox);
        }
    }

    private int GetVisiblePaymentRowCount()
    {
        return 1 + _extraPaymentBoxes.Count;
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
        CardAmount = GetNonCashPaymentSum();
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
            CardAmount = GetNonCashPaymentSum();
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
            ReceivedText.Text = $"{ReceivedAmount:0.00}";
            var remainder = TotalDue - ReceivedAmount;
            RemainderLabel.Text = remainder > 0 ? "Остаток" : "Сдача";
            RemainderText.Text = $"{Math.Abs(remainder):0.00}";
            ConfirmButtonText.Text = $"Оплатить {Math.Min(ReceivedAmount, TotalDue):0.##} c";

            RemainderLabel.Foreground = BrushFrom("#344054");
            RemainderText.Foreground = remainder <= 0 ? BrushFrom("#23A455") : BrushFrom("#FF2B22");
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
        if (_extraPaymentBoxes.Count == 0)
        {
            CashPaymentBox.Text = FormatInput(TotalDue);
        }
    }

    private decimal GetEnteredPaymentSum()
    {
        return ParseAmount(CashPaymentBox.Text) + _extraPaymentBoxes.Sum(box => ParseAmount(box.Text));
    }

    private decimal GetNonCashPaymentSum()
    {
        return _extraPaymentBoxes.Sum(box => ParseAmount(box.Text));
    }

    private IReadOnlyList<PaymentLine> GetPaymentLines()
    {
        var lines = new List<PaymentLine>();
        var cashAmount = ParseAmount(CashPaymentBox.Text);
        if (cashAmount > 0)
        {
            lines.Add(new PaymentLine(CashId, cashAmount));
        }

        for (var i = 0; i < _extraPaymentBoxes.Count; i++)
        {
            var amount = ParseAmount(_extraPaymentBoxes[i].Text);
            if (amount <= 0)
            {
                continue;
            }

            var cashId = _extraPaymentCashBoxes.ElementAtOrDefault(i)?.SelectedItem is Cash cash
                ? cash.Id
                : CashId;
            lines.Add(new PaymentLine(cashId, amount));
        }

        return lines;
    }

    private TextBox? GetActiveTextBox()
    {
        return _activeInput switch
        {
            "discountPercent" => DiscountPercentBox,
            "discountAmount" => DiscountAmountBox,
            "note" => NoteBox,
            "extra" => _activeExtraPaymentBox ?? CashPaymentBox,
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
            "extra" => "Введите сумму оплаты",
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

    private UserSettings ParseUserSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserSettings { CheckPrint = _settings.CheckPrint };
        }

        return UserSettings.Parse(json, _settings.CheckPrint);
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
