using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FFPOS.Models;

public class Product : INotifyPropertyChanged
{
    private int _selectedQuantity;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Image { get; set; }
    public string? ImagePath { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    [JsonPropertyName("pos_view")]
    public int PosView { get; set; }
    public int Status { get; set; } = 1;
    [JsonPropertyName("unit_id")]
    public int UnitId { get; set; }
    public string? Unit { get; set; }
    public int CategoryId { get; set; }
    public string? Category { get; set; }
    public decimal Quantity { get; set; }
    public decimal Package { get; set; } = 1;
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }
    [JsonPropertyName("is_active")]
    public int IsActive { get; set; } = 1;
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    public int SelectedQuantity
    {
        get => _selectedQuantity;
        set
        {
            if (_selectedQuantity == value)
            {
                return;
            }

            _selectedQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedQuantity));
        }
    }

    public bool HasSelectedQuantity => SelectedQuantity > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
