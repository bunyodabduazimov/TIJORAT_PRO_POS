using System;
using System.Drawing.Printing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Controls;
using FFPOS.Models;
using FFPOS.Services;
using MySqlConnector;

namespace FFPOS.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsService _settingsService = new();
    private readonly DatabaseService _databaseService = new();
    private readonly List<Store> _stores = new();
    private AppActivationSettings _settings = new();

    public bool WasReset { get; private set; }

    public SettingsWindow()
    {
        InitializeComponent();
        FiscalModuleToggle.Toggled += FiscalModuleToggle_Toggled;
        DatabaseTypeBox.SelectionChanged += DatabaseTypeBox_SelectionChanged;
    }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        _settings = _settingsService.Load();
        _databaseService.InitializeAsync().GetAwaiter().GetResult();

        BaseUrlBox.Text = _settings.BaseUrl;
        PublicUrlBox.Text = _settings.PublicUrl;
        SQLitePathBox.Text = _databaseService.DatabasePath;
        StoreNameBox.Text = string.IsNullOrWhiteSpace(_settings.AppName) ? "TIJORAT PRO POS" : _settings.AppName;
        StorePhoneBox.Text = string.IsNullOrWhiteSpace(_settings.AppPhone) ? "-" : _settings.AppPhone;
        AppDateBox.Text = string.IsNullOrWhiteSpace(_settings.AppDate) ? "-" : _settings.AppDate;
        AppStatusTextBox.Text = _settings.IsActivated ? "РђРєС‚РёРІРµРЅ" : "РћС‚РєР»СЋС‡РµРЅ";

        SetCheck(IsTouchScreenBox, _settings.IsTouchScreen);
        SetCheck(TotalSummaBox, _settings.TotalSumma);
        SetCheck(ColumnQtyStockBox, _settings.QtyStock);
        SetCheck(NewRowBox, _settings.NewRow);
        SetCheck(EditPriceBox, _settings.EditPrice);
        SetCheck(CheckPrintBox, _settings.CheckPrint);
        SetCheck(MessagErrorBox, _settings.MessageError);
        SetCheck(DiscountBox, _settings.Discount);
        SetCheck(ReturnSaleBox, _settings.ReturnSale);
        SetCheck(PeopleShowBox, _settings.PeopleShow);
        SetCheck(PeoplePayBox, _settings.PeoplePay);
        SetCheck(DebtSaleBox, _settings.DebtSale);
        SetCheck(DeleteRowBox, _settings.DeleteRow);

        FiscalModuleToggle.IsChecked = _settings.FiscalPrint;
        FiscalPrintCheckBox.IsChecked = _settings.FiscalPrintCheck;
        PrinterIpBox.Text = _settings.PrinterIP;
        PageWidthBox.Text = _settings.PageWidth.ToString();
        TaxTypeBox.Text = _settings.TaxType;
        VatCodeBox.Text = _settings.VatCode;
        CommodityBox.Text = _settings.Commodity;
        AdminCodeBox.Text = _settings.AdminCode;
        SyncTimeInminBox.Value = _settings.SyncTimeInmin;
        SyncDayBox.Value = _settings.SyncDay;
        MaxDiscountBox.Value = _settings.MaxDiscount;
        FocusQtyBox.SelectedValue = _settings.FocusQty == 2 ? 2 : 1;
        AppTypeBox.SelectedValue = _settings.AppType is >= 1 and <= 3 ? _settings.AppType : 1;
        DatabaseTypeBox.SelectedValue = _settings.DatabaseType is >= 1 and <= 2 ? _settings.DatabaseType : 1;

        MySqlHostBox.Text = _settings.MySqlHost;
        MySqlPortBox.Text = _settings.MySqlPort.ToString();
        MySqlDatabaseBox.Text = string.IsNullOrWhiteSpace(_settings.MySqlDatabase) ? "local_db" : _settings.MySqlDatabase;
        MySqlUsernameBox.Text = string.IsNullOrWhiteSpace(_settings.MySqlUsername) ? "root" : _settings.MySqlUsername;
        MySqlPasswordBox.Text = _settings.MySqlPassword;

        LoadStores();
        PopulatePrinters();
        SelectPrinter(_settings.PrinterNameDefault);

        UpdateSummary();
        UpdateStoreDetails();
        UpdateDatabaseVisibility();
        UpdateFiscalVisibility();

        ErrorText.Text = string.Empty;
        ErrorText.Foreground = new SolidColorBrush(Color.FromRgb(249, 31, 37));
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryApplySettings(out var error))
        {
            ErrorText.Foreground = new SolidColorBrush(Color.FromRgb(249, 31, 37));
            ErrorText.Text = error;
            return;
        }

        _settingsService.Save(_settings);
        UpdateSummary();
        AppDialogWindow.ShowSuccess("РќР°СЃС‚СЂРѕР№РєРё СѓСЃРїРµС€РЅРѕ СЃРѕС…СЂР°РЅРµРЅС‹", "РЎРѕС…СЂР°РЅРµРЅРѕ", this);
        Close();
    }

    private bool TryApplySettings(out string error)
    {
        error = string.Empty;

        if (!TryGetInt(PageWidthBox.Text, out var pageWidth))
        {
            error = "РџСЂРѕРІРµСЂСЊС‚Рµ С‡РёСЃР»РѕРІС‹Рµ РїРѕР»СЏ";
            return false;
        }

        var syncTimeInmin = SyncTimeInminBox.Value;
        var syncDay = SyncDayBox.Value;
        var maxDiscount = MaxDiscountBox.Value;
        var focusQty = FocusQtyBox.SelectedValue;

        if (syncTimeInmin < 5 || syncTimeInmin > 1440)
        {
            error = "РРЅС‚РµСЂРІР°Р» СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёРё РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ РѕС‚ 5 РґРѕ 1440 РјРёРЅСѓС‚";
            return false;
        }

        if (syncDay < 0 || syncDay > 30)
        {
            error = "РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ Р·Р° РґРЅРµР№ РґРѕР»Р¶РЅР° Р±С‹С‚СЊ РѕС‚ 0 РґРѕ 30";
            return false;
        }

        if (maxDiscount < 0 || maxDiscount > 100)
        {
            error = "РњР°РєСЃРёРјР°Р»СЊРЅР°СЏ СЃРєРёРґРєР° РґРѕР»Р¶РЅР° Р±С‹С‚СЊ РѕС‚ 0 РґРѕ 100";
            return false;
        }

        if (string.IsNullOrWhiteSpace(BaseUrlBox.Text))
        {
            error = "BaseUrl РѕР±СЏР·Р°С‚РµР»РµРЅ";
            return false;
        }

        var databaseType = DatabaseTypeBox.SelectedValue is >= 1 and <= 2 ? DatabaseTypeBox.SelectedValue : 1;
        if (databaseType == 2)
        {
            if (string.IsNullOrWhiteSpace(MySqlHostBox.Text) ||
                string.IsNullOrWhiteSpace(MySqlDatabaseBox.Text) ||
                string.IsNullOrWhiteSpace(MySqlUsernameBox.Text))
            {
                error = "Р—Р°РїРѕР»РЅРёС‚Рµ РїР°СЂР°РјРµС‚СЂС‹ MySQL";
                return false;
            }

            if (!TryGetInt(MySqlPortBox.Text, out var mysqlPort) || mysqlPort < 1 || mysqlPort > 65535)
            {
                error = "РџРѕСЂС‚ MySQL РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ РѕС‚ 1 РґРѕ 65535";
                return false;
            }
        }

        _settings.BaseUrl = BaseUrlBox.Text.Trim();
        _settings.PublicUrl = string.IsNullOrWhiteSpace(PublicUrlBox.Text) ? "/public" : PublicUrlBox.Text.Trim();
        _settings.DatabaseType = databaseType;
        _settings.CheckPrint = GetCheck(CheckPrintBox);
        _settings.FiscalPrint = FiscalModuleToggle.IsChecked;
        _settings.FiscalPrintCheck = GetCheck(FiscalPrintCheckBox);
        _settings.PageWidth = pageWidth;
        _settings.SyncTimeInmin = syncTimeInmin;
        _settings.SyncDay = syncDay;
        _settings.MaxDiscount = maxDiscount;
        _settings.Discount = GetCheck(DiscountBox);
        _settings.FocusQty = focusQty;

        _settings.PrinterNameDefault = GetSelectedPrinter();
        _settings.PrinterIP = PrinterIpBox.Text.Trim();
        _settings.TaxType = TaxTypeBox.Text.Trim();
        _settings.VatCode = VatCodeBox.Text.Trim();
        _settings.Commodity = CommodityBox.Text.Trim();
        _settings.AdminCode = string.IsNullOrWhiteSpace(AdminCodeBox.Text) ? "2244" : AdminCodeBox.Text.Trim();

        _settings.IsTouchScreen = GetCheck(IsTouchScreenBox);
        _settings.TotalSumma = GetCheck(TotalSummaBox);
        _settings.QtyStock = GetCheck(ColumnQtyStockBox);
        _settings.NewRow = GetCheck(NewRowBox);
        _settings.AppType = AppTypeBox.SelectedValue is >= 1 and <= 3 ? AppTypeBox.SelectedValue : 1;
        _settings.EditPrice = GetCheck(EditPriceBox);
        _settings.MessageError = GetCheck(MessagErrorBox);
        _settings.ReturnSale = GetCheck(ReturnSaleBox);
        _settings.PeopleShow = GetCheck(PeopleShowBox);
        _settings.PeoplePay = GetCheck(PeoplePayBox);
        _settings.DebtSale = GetCheck(DebtSaleBox);
        _settings.DeleteRow = GetCheck(DeleteRowBox);

        _settings.MySqlHost = MySqlHostBox.Text.Trim();
        _settings.MySqlPort = TryGetInt(MySqlPortBox.Text, out var mysqlPortValue) ? mysqlPortValue : 3306;
        _settings.MySqlDatabase = MySqlDatabaseBox.Text.Trim();
        _settings.MySqlUsername = MySqlUsernameBox.Text.Trim();
        _settings.MySqlPassword = MySqlPasswordBox.Text;
        _settings.StoreId = GetSelectedStoreId();

        return true;
    }

    private void ReloadClicked(object sender, RoutedEventArgs e)
    {
        var confirmed = AppDialogWindow.Confirm(
            "РЎР±СЂРѕСЃРёС‚СЊ РІСЃРµ РЅР°СЃС‚СЂРѕР№РєРё? РџСЂРѕРіСЂР°РјРјР° РІРµСЂРЅРµС‚СЃСЏ Рє РїРµСЂРІРѕРјСѓ Р·Р°РїСѓСЃРєСѓ Рё РїРѕРїСЂРѕСЃРёС‚ СЃРЅРѕРІР° РїРѕРґС‚РІРµСЂРґРёС‚СЊ РЅРѕРјРµСЂ С‚РµР»РµС„РѕРЅР°.",
            "РЎР±СЂРѕСЃ РЅР°СЃС‚СЂРѕРµРє",
            "РЎР±СЂРѕСЃРёС‚СЊ",
            "РћС‚РјРµРЅР°",
            this);

        if (!confirmed)
        {
            return;
        }

        _settingsService.Reset();
        WasReset = true;
        AppDialogWindow.ShowSuccess("РќР°СЃС‚СЂРѕР№РєРё СЃР±СЂРѕС€РµРЅС‹. Р’С‹РїРѕР»РЅСЏРµС‚СЃСЏ РїРѕРІС‚РѕСЂРЅР°СЏ Р°РєС‚РёРІР°С†РёСЏ.", "РЎР±СЂРѕС€РµРЅРѕ", this);
        DialogResult = false;
        Close();
    }

    private void FiscalModuleToggle_Toggled(object? sender, EventArgs e)
    {
        UpdateFiscalVisibility();
    }

    private void DatabaseTypeBox_SelectionChanged(object? sender, EventArgs e)
    {
        UpdateDatabaseVisibility();
    }

    private async void TestMySqlClicked(object sender, RoutedEventArgs e)
    {
        if (!ValidateMySqlFields(out var errorMessage))
        {
            AppDialogWindow.ShowError(errorMessage, "РћС€РёР±РєР° РїСЂРѕРІРµСЂРєРё", this);
            return;
        }

        var host = MySqlHostBox.Text.Trim();
        var port = MySqlPortBox.Text.Trim();
        var database = MySqlDatabaseBox.Text.Trim();
        var username = MySqlUsernameBox.Text.Trim();
        var password = MySqlPasswordBox.Text.Trim();

        TestMySqlButton.IsEnabled = false;
        try
        {
            var result = await Task.Run(() => TestMySqlConnection(host, port, database, username, password));
            if (result.Success)
            {
                AppDialogWindow.ShowSuccess(
                    $"РџРѕРґРєР»СЋС‡РµРЅРёРµ РІС‹РїРѕР»РЅРµРЅРѕ СѓСЃРїРµС€РЅРѕ.\nР”СЂР°Р№РІРµСЂ: {result.DriverName}",
                    "MySQL РїРѕРґРєР»СЋС‡РµРЅРёРµ",
                    this);
                return;
            }

            AppDialogWindow.ShowError(result.ErrorMessage, "РћС€РёР±РєР° РїРѕРґРєР»СЋС‡РµРЅРёСЏ", this);
        }
        finally
        {
            TestMySqlButton.IsEnabled = true;
        }
    }

    private void StoreSelectBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateStoreDetails();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private void UpdateSummary()
    {
        HeaderSubtitle.Text = !string.IsNullOrWhiteSpace(_settings.AppName)
            ? $"{_settings.AppName} вЂў {(_settings.IsActivated ? "РђРєС‚РёРІРµРЅ" : "РћС‚РєР»СЋС‡РµРЅ")}"
            : "Р’СЃРµ РїР°СЂР°РјРµС‚СЂС‹ POS РІ РѕРґРЅРѕРј РјРµСЃС‚Рµ";
    }

    private void UpdateFiscalVisibility()
    {
        var active = FiscalModuleToggle.IsChecked;
        FiscalStatusText.Text = active ? "РђРєС‚РёРІРµРЅ" : "РћС‚РєР»СЋС‡РµРЅ";
        FiscalStatusText.Foreground = active
            ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
            : new SolidColorBrush(Color.FromRgb(249, 31, 37));
        FiscalDetailsPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDatabaseVisibility()
    {
        var remote = DatabaseTypeBox.SelectedValue == 2;
        MySqlDetailsPanel.Visibility = remote ? Visibility.Visible : Visibility.Collapsed;
        SqliteDetailsPanel.Visibility = remote ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool ValidateMySqlFields(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(MySqlHostBox.Text) ||
            string.IsNullOrWhiteSpace(MySqlDatabaseBox.Text) ||
            string.IsNullOrWhiteSpace(MySqlUsernameBox.Text))
        {
            error = "Р—Р°РїРѕР»РЅРёС‚Рµ РѕР±СЏР·Р°С‚РµР»СЊРЅС‹Рµ РїРѕР»СЏ РїРѕРґРєР»СЋС‡РµРЅРёСЏ MySQL.";
            return false;
        }

        if (!TryGetInt(MySqlPortBox.Text, out var port) || port < 1 || port > 65535)
        {
            error = "РџРѕСЂС‚ MySQL РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ РѕС‚ 1 РґРѕ 65535.";
            return false;
        }

        return true;
    }

    private (bool Success, string DriverName, string ErrorMessage) TestMySqlConnection(
        string host,
        string port,
        string database,
        string username,
        string password)
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = uint.TryParse(port, out var parsedPort) ? parsedPort : 3306u,
                Database = database,
                UserID = username,
                ConnectionTimeout = 5,
                SslMode = MySqlSslMode.None
            };

            if (!string.IsNullOrWhiteSpace(password))
            {
                builder.Password = password;
            }

            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            _ = command.ExecuteScalar();

            return (true, "MySqlConnector", string.Empty);
        }
        catch (Exception ex)
        {
            return (false, "MySqlConnector", ex.Message);
        }
    }
    private void LoadStores()
    {
        _stores.Clear();
        _stores.AddRange(_databaseService.GetStoresAsync().GetAwaiter().GetResult());

        StoreSelectBox.ItemsSource = null;
        StoreSelectBox.ItemsSource = _stores;

        if (_stores.Count == 0)
        {
            StoreSelectBox.SelectedIndex = -1;
            UpdateStoreDetails();
            return;
        }

        var selectedStore = _stores.FirstOrDefault(store => store.Id == _settings.StoreId)
            ?? _stores.FirstOrDefault();

        StoreSelectBox.SelectedItem = selectedStore;
        StoreSelectBox.SelectedIndex = selectedStore is null ? -1 : _stores.IndexOf(selectedStore);
        if (StoreSelectBox.SelectedIndex < 0 && _stores.Count > 0)
        {
            StoreSelectBox.SelectedIndex = 0;
        }

        if (_settings.StoreId <= 0 && StoreSelectBox.SelectedItem is Store store)
        {
            _settings.StoreId = store.Id;
        }
    }

    private void UpdateStoreDetails()
    {
        var store = StoreSelectBox.SelectedItem as Store ?? _stores.FirstOrDefault();

        StoreNameInfoBox.Text = store?.Name ?? "-";
        StoreAgelNameBox.Text = store?.AgelName ?? "-";
        StoreLocationBox.Text = store?.Location ?? "-";
        StorePhoneInfoBox.Text = store?.Phone ?? "-";
        StoreEmailBox.Text = store?.Email ?? "-";
        StoreSiteBox.Text = store?.Site ?? "-";
        StoreDescriptionBox.Text = store?.Description ?? "-";

        if (store is not null && _settings.StoreId <= 0)
        {
            _settings.StoreId = store.Id;
        }
    }

    private int GetSelectedStoreId()
    {
        return StoreSelectBox.SelectedItem is Store store ? store.Id : (_stores.FirstOrDefault()?.Id ?? 1);
    }

    private void PopulatePrinters()
    {
        PrinterNameDefaultBox.Items.Clear();

        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            PrinterNameDefaultBox.Items.Add(printerName);
        }

        if (PrinterNameDefaultBox.Items.Count > 0 && PrinterNameDefaultBox.SelectedIndex < 0)
        {
            PrinterNameDefaultBox.SelectedIndex = 0;
        }
    }

    private void SelectPrinter(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return;
        }

        foreach (var item in PrinterNameDefaultBox.Items.OfType<string>())
        {
            if (string.Equals(item, printerName, StringComparison.OrdinalIgnoreCase))
            {
                PrinterNameDefaultBox.SelectedItem = item;
                return;
            }
        }
    }

    private string GetSelectedPrinter()
    {
        return PrinterNameDefaultBox.SelectedItem?.ToString() ?? PrinterNameDefaultBox.Text.Trim();
    }

    private static void SetCheck(SettingToggleField toggleField, bool value)
    {
        toggleField.IsChecked = value;
    }

    private static bool GetCheck(SettingToggleField toggleField)
    {
        return toggleField.IsChecked;
    }

    private static bool TryGetInt(string? value, out int result)
    {
        return int.TryParse(value?.Trim(), out result);
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}

