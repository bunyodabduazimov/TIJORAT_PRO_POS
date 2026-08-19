using System.Drawing.Printing;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class UserSettingsWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DatabaseService _databaseService = new();
    private readonly User _user;
    private readonly List<Stock> _stocks = new();
    private readonly List<Cash> _cashes = new();
    private UserSettings _settings = new();

    public UserSettingsWindow(User user)
    {
        _user = user;
        InitializeComponent();
    }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        _databaseService.InitializeAsync().GetAwaiter().GetResult();
        _settings = ParseSettings(_user.Settings);

        if (_settings.DefaultStockId <= 0)
        {
            _settings.DefaultStockId = _user.StockId;
        }

        if (_settings.DefaultCashId <= 0)
        {
            _settings.DefaultCashId = _user.CashId;
        }

        UserNameBox.Text = string.IsNullOrWhiteSpace(_user.Name) ? "-" : _user.Name;
        UserLoginBox.Text = string.IsNullOrWhiteSpace(_user.Username) ? "-" : _user.Username;

        LoadStocks();
        LoadCashes();
        PopulatePrinters();

        SelectById(StockBox, _settings.DefaultStockId);
        SelectById(CashBox, _settings.DefaultCashId);
        SelectPrinter(_settings.PrinterNameDefault);

        CheckPrintBox.IsChecked = _settings.CheckPrint;
        ShowProductImageBox.IsChecked = _settings.ShowProductImage;
        ShowSkuBox.IsChecked = _settings.ShowSku;
        ShowBarcodeBox.IsChecked = _settings.ShowBarcode;
        ShowStockQuantityBox.IsChecked = _settings.ShowStockQuantity;
        TableCompactModeBox.IsChecked = _settings.TableCompactMode;
        AutoSyncBox.IsChecked = _settings.AutoSync;
        SyncAfterSaleBox.IsChecked = _settings.SyncAfterSale;
        SyncIntervalBox.Value = _settings.SyncIntervalMinutes is >= 5 and <= 1440
            ? _settings.SyncIntervalMinutes
            : 60;
        ConfirmBeforeLogoutBox.IsChecked = _settings.ConfirmBeforeLogout;
        NoteBox.Text = _settings.Note;

        ErrorText.Text = string.Empty;
    }

    private void LoadStocks()
    {
        _stocks.Clear();
        _stocks.AddRange(_databaseService.GetStocksAsync().GetAwaiter().GetResult());
        StockBox.ItemsSource = null;
        StockBox.ItemsSource = _stocks;

        if (_stocks.Count > 0 && StockBox.SelectedIndex < 0)
        {
            StockBox.SelectedIndex = 0;
        }
    }

    private void LoadCashes()
    {
        _cashes.Clear();
        _cashes.AddRange(_databaseService.GetCashesAsync().GetAwaiter().GetResult());
        CashBox.ItemsSource = null;
        CashBox.ItemsSource = _cashes;

        if (_cashes.Count > 0 && CashBox.SelectedIndex < 0)
        {
            CashBox.SelectedIndex = 0;
        }
    }

    private void PopulatePrinters()
    {
        PrinterBox.Items.Clear();
        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            PrinterBox.Items.Add(printerName);
        }

        if (PrinterBox.Items.Count > 0 && PrinterBox.SelectedIndex < 0)
        {
            PrinterBox.SelectedIndex = 0;
        }
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryApplySettings(out var error))
        {
            ErrorText.Text = error;
            return;
        }

        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        _user.StockId = _settings.DefaultStockId;
        _user.CashId = _settings.DefaultCashId;
        _user.Settings = json;

        _databaseService.SaveUserSettingsAsync(_user, json).GetAwaiter().GetResult();
        AppDialogWindow.ShowSuccess("Настройки пользователя сохранены", "Сохранено", this);
        DialogResult = true;
        Close();
    }

    private bool TryApplySettings(out string error)
    {
        error = string.Empty;

        if (StockBox.SelectedItem is not Stock selectedStock)
        {
            error = "Выберите склад по умолчанию";
            return false;
        }

        if (CashBox.SelectedItem is not Cash selectedCash)
        {
            error = "Выберите счёт зачисления";
            return false;
        }

        if (SyncIntervalBox.Value < 5 || SyncIntervalBox.Value > 1440)
        {
            error = "Интервал синхронизации должен быть от 5 до 1440 минут";
            return false;
        }

        _settings.DefaultStockId = selectedStock.Id;
        _settings.DefaultCashId = selectedCash.Id;
        _settings.PrinterNameDefault = PrinterBox.SelectedItem?.ToString() ?? PrinterBox.Text.Trim();
        _settings.CheckPrint = CheckPrintBox.IsChecked;
        _settings.ShowProductImage = ShowProductImageBox.IsChecked;
        _settings.ShowSku = ShowSkuBox.IsChecked;
        _settings.ShowBarcode = ShowBarcodeBox.IsChecked;
        _settings.ShowStockQuantity = ShowStockQuantityBox.IsChecked;
        _settings.TableCompactMode = TableCompactModeBox.IsChecked;
        _settings.AutoSync = AutoSyncBox.IsChecked;
        _settings.SyncAfterSale = SyncAfterSaleBox.IsChecked;
        _settings.SyncIntervalMinutes = SyncIntervalBox.Value;
        _settings.ConfirmBeforeLogout = ConfirmBeforeLogoutBox.IsChecked;
        _settings.Note = NoteBox.Text.Trim();

        return true;
    }

    private void CancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private static UserSettings ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserSettings();
        }

        return UserSettings.Parse(json);
    }

    private static void SelectById(ComboBox comboBox, int id)
    {
        if (id <= 0)
        {
            return;
        }

        foreach (var item in comboBox.Items)
        {
            var itemId = item switch
            {
                Stock stock => stock.Id,
                Cash cash => cash.Id,
                _ => 0
            };

            if (itemId == id)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void SelectPrinter(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return;
        }

        foreach (var item in PrinterBox.Items.OfType<string>())
        {
            if (string.Equals(item, printerName, StringComparison.OrdinalIgnoreCase))
            {
                PrinterBox.SelectedItem = item;
                return;
            }
        }
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
