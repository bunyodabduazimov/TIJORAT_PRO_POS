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
    public decimal Price { get; set; }
    public decimal Discount { get; set; }
    public decimal Bonus { get; set; }

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
        }
    }

    public decimal Total => (Price > 0 ? Price : Product.Price) * Quantity;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
