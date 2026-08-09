using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class Store
{
    public int Id { get; set; }
    public string? Name { get; set; }
    [JsonPropertyName("agel_name")]
    public string? AgelName { get; set; }
    public string? Location { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Site { get; set; }
    public string? Description { get; set; }
    public string? Settings { get; set; }
    public int Status { get; set; }
}
