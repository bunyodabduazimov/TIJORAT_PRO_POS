namespace FFPOS.Models;

public sealed class UserSettings
{
    public int DefaultStockId { get; set; }
    public int DefaultCashId { get; set; }
    public string PrinterNameDefault { get; set; } = string.Empty;
    public bool CheckPrint { get; set; } = true;
    public bool ShowProductImage { get; set; } = true;
    public bool ShowBarcode { get; set; } = true;
    public bool ShowSku { get; set; } = true;
    public bool ShowStockQuantity { get; set; } = true;
    public bool TableCompactMode { get; set; } = true;
    public bool AutoSync { get; set; } = true;
    public bool SyncAfterSale { get; set; }
    public int SyncIntervalMinutes { get; set; } = 60;
    public bool ConfirmBeforeLogout { get; set; } = true;
    public string Note { get; set; } = string.Empty;
}
