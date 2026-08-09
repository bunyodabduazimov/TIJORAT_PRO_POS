using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class User
{
    public int Id { get; set; }

    [JsonPropertyName("store_id")]
    public int StoreId { get; set; }

    [JsonPropertyName("cash_id")]
    public int CashId { get; set; }

    [JsonPropertyName("stock_id")]
    public int StockId { get; set; }

    public string? Name { get; set; }
    public string? Username { get; set; }

    [JsonPropertyName("pincode")]
    public string? Pincode { get; set; }

    public string? Settings { get; set; }
    public int Status { get; set; }
}
