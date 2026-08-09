using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class Category : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("parent_id")]
    public int ParentId { get; set; }
    public string? Image { get; set; }
    public string? IconPath { get; set; }
    public string IconGlyph { get; set; } = "\uE7BF";
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }
    public int Status { get; set; } = 1;
    [JsonPropertyName("is_active")]
    public int IsActiveValue { get; set; } = 1;
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
