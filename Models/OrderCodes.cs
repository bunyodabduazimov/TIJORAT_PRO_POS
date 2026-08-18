namespace FFPOS.Models;

public static class OrderCodes
{
    public const string TakeAwayTypeCode = "1";
    public const string DeliveryTypeCode = "2";
    public const string DineInTypeCode = "3";
    public const string OpenStatusCode = "1";
    public const string PaidStatusCode = "2";

    public const string TakeAwayTypeName = "С собой";
    public const string DeliveryTypeName = "Доставка";
    public const string DineInTypeName = "В зале";
    public const string OpenStatusName = "open";
    public const string PaidStatusName = "paid";

    public static string ToOrderTypeCode(string? value)
    {
        return Normalize(value) switch
        {
            TakeAwayTypeCode or "с собой" => TakeAwayTypeCode,
            DeliveryTypeCode or "доставка" => DeliveryTypeCode,
            DineInTypeCode or "в зале" => DineInTypeCode,
            _ => DineInTypeCode
        };
    }

    public static string ToOrderTypeName(string? value)
    {
        return Normalize(value) switch
        {
            TakeAwayTypeCode or "с собой" => TakeAwayTypeName,
            DeliveryTypeCode or "доставка" => DeliveryTypeName,
            DineInTypeCode or "в зале" => DineInTypeName,
            _ => DineInTypeName
        };
    }

    public static string ToStatusCode(string? value)
    {
        return Normalize(value) switch
        {
            PaidStatusCode or "paid" or "оплачен" => PaidStatusCode,
            OpenStatusCode or "open" or "открыт" => OpenStatusCode,
            _ => OpenStatusCode
        };
    }

    public static string ToStatusName(string? value)
    {
        return Normalize(value) switch
        {
            PaidStatusCode or "paid" or "оплачен" => PaidStatusName,
            OpenStatusCode or "open" or "открыт" => OpenStatusName,
            _ => OpenStatusName
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
