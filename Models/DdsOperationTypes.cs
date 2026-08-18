namespace FFPOS.Models;

public static class DdsOperationTypes
{
    public const string SalePayment = "salepay";
    public const string CustomerIn = "customer_in";
    public const string CustomerOut = "customer_out";
    public const string CashIn = "cash_in";
    public const string CashOut = "cash_out";

    public static string GetTitle(string? orderType)
    {
        return orderType switch
        {
            SalePayment => "Платеж продажи",
            CustomerIn => "Платеж от контрагента",
            CustomerOut => "Платеж контрагенту",
            CashIn => "Приходный ордер",
            CashOut => "Расходный ордер",
            _ => "Платеж"
        };
    }

    public static bool IsIncome(string? orderType)
    {
        return orderType is SalePayment or CustomerIn or CashIn;
    }

    public static bool RequiresCustomer(string? orderType)
    {
        return orderType is CustomerIn or CustomerOut;
    }
}
