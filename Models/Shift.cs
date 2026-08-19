namespace FFPOS.Models;

public class Shift
{
    public int Id { get; set; }
    public int StoreId { get; set; } = 1;
    public int CashId { get; set; } = 1;
    public int OpenedByUserId { get; set; } = 1;
    public int? ClosedByUserId { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal ReturnTotal { get; set; }
    public decimal SalePaymentTotal { get; set; }
    public decimal PaymentIncomeTotal { get; set; }
    public decimal PaymentExpenseTotal { get; set; }
    public decimal PaymentTotal { get; set; }
    public decimal CashInTotal { get; set; }
    public decimal CashOutTotal { get; set; }
    public decimal ClosingBalance { get; set; }
    public int SalesCount { get; set; }
    public int PaymentCount { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(24);
    public DateTime? ClosedAt { get; set; }
    public string? Note { get; set; }
    public int Status { get; set; } = 1;

    public bool IsOpen => Status == 1 && ClosedAt is null;
    public bool IsExpired => IsOpen && DateTime.Now >= ExpiresAt;
    public TimeSpan RemainingTime => IsOpen ? ExpiresAt - DateTime.Now : TimeSpan.Zero;
}
