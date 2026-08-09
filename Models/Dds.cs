namespace FFPOS.Models;

public class Dds
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public int UserId { get; set; }
    public int CashId { get; set; }
    public int PeopleId { get; set; }
    public decimal Summa { get; set; }
    public long EventTime { get; set; }
    public string? Description { get; set; }
    public string Date { get; set; } = string.Empty;
    public int Status { get; set; }
}
