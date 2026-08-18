namespace FFPOS.Models;

public class Dds
{
    public int Id { get; set; }
    public int DocId { get; set; }
    public int StoreId { get; set; }
    public int UserId { get; set; }
    public int CashId { get; set; }
    public int PeopleId { get; set; }
    public int ArticleId { get; set; }
    public decimal Summa { get; set; }
    public long EventTime { get; set; }
    public string OrderType { get; set; } = "salepay";
    public string? Description { get; set; }
    public string Date { get; set; } = string.Empty;
    public int Status { get; set; }
    public int SyncStatus { get; set; }
    public int? ServerId { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? SyncError { get; set; }
}
