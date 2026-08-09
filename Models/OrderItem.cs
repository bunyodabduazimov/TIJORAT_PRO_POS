using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FFPOS.Models;

public class OrderItem : INotifyPropertyChanged
{
    private int _quantity;

    public int Id { get; set; }
    public int OrderNumber { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = new();
    public string? Note { get; set; }
    public decimal Price { get; set; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value)
            {
                return;
            }

            _quantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(Description));
        }
    }

    public decimal Total => (Price > 0 ? Price : Product.Price) * Quantity;
    public string Description => Product.CategoryId == 4 ? "Средняя" : "Обычный";
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
