using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class PriceData
{
    public int Id { get; set; }

    [JsonPropertyName("price_id")]
    public int PriceId { get; set; }

    [JsonPropertyName("product_id")]
    public int ProductId { get; set; }

    public decimal Price { get; set; }
    public decimal Bonus { get; set; }
    public decimal Discount { get; set; }
}
