using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class AppActivationSettings
{
    public string BaseUrl { get; set; } = "https://app.tijorat.pro";
    public string AppId { get; set; } = string.Empty;
    public string AppName { get; set; } = "TIJORAT PRO";
    public string AppPhone { get; set; } = string.Empty;
    public int AppType { get; set; } = 1;
    public string AppDate { get; set; } = string.Empty;
    public bool IsActivated { get; set; } = false;
    public string PublicUrl { get; set; } = string.Empty;
    public int StoreId { get; set; } = 1;
    public int DatabaseType { get; set; } = 1;
    public string MySqlHost { get; set; } = "127.0.0.1";
    public int MySqlPort { get; set; } = 3306;
    public string MySqlDatabase { get; set; } = "local_db";
    public string MySqlUsername { get; set; } = "root";
    public string MySqlPassword { get; set; } = string.Empty;
    public int PeopleId { get; set; } = 1;
    public int ProductDefaultId { get; set; } = 1;
    public string BarcodeStart { get; set; } = "22";
    public bool FiscalPrint { get; set; } = false;
    public bool FiscalPrintCheck { get; set; } = true;
    public string PrinterIP { get; set; } = "192.168.1.199";
    public string TaxType { get; set; } = "GENERAL";
    public string VatCode { get; set; } = "STANDARD";
    public string Commodity { get; set; } = "GOODS";
    public string PrinterNameDefault { get; set; } = "Microsoft Print to PDF";
    public bool CheckPrint { get; set; }
    public bool MessageError { get; set; } = false;
    public int SyncTimeInmin { get; set; } = 60;
    public int SyncDay { get; set; } = 10;
    public bool EditPrice { get; set; } = true;
    public bool Discount { get; set; } = true;
    public int MaxDiscount { get; set; } = 10;
    public bool QtyStock { get; set; } = false;
    public bool IsTouchScreen { get; set; } = true;
    public bool TotalSumma { get; set; } = true;
    public int FocusQty { get; set; } = 2;
    public bool NewRow { get; set; } = false;
    public bool ReturnSale { get; set; } = true;
    public bool PeopleShow { get; set; } = true;
    public bool PeoplePay { get; set; } = true;
    public bool DebtSale { get; set; } = true;
    public bool DeleteRow { get; set; } = true;

    public string AdminCode { get; set; } = "2244";

    [JsonIgnore]
    public AppInfo? App { get; set; }

    [JsonIgnore]
    public int PriceId { get; set; } = 1;

    [JsonIgnore]
    public int StockId { get; set; } = 1;

    [JsonIgnore]
    public int PageWidth { get; set; } = 58;

    [JsonIgnore]
    public int AppStatus => IsActivated ? 1 : 0;

    [JsonIgnore]
    public string EffectiveApiBaseUrl => BaseUrl;

    [JsonIgnore]
    public string EffectiveAppId => AppId;

    public void ApplyApp(AppInfo app)
    {
        App = app;
        AppId = app.AppId;
        AppName = app.Name;
        AppPhone = app.Phone;
        AppDate = app.DateTo;
        FiscalPrint = app.Fiscat == 1;
        IsActivated = string.Equals(app.Status, "\u0410\u043a\u0442\u0438\u0432\u0435\u043d", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(app.Status, "active", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(app.AppId);
    }
}

public class AppInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string DateTo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Fiscat { get; set; }
    public string AppId { get; set; } = string.Empty;
}
