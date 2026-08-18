using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class Article
{
    public int Id { get; set; }
    [JsonPropertyName("parent_id")]
    public int ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    [JsonPropertyName("check")]
    public int Check { get; set; }
    public string Type { get; set; } = string.Empty;
}
