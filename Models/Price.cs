using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class Price
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Status { get; set; }

    [JsonPropertyName("price_data")]
    public List<PriceData> PriceData { get; set; } = new();
}
