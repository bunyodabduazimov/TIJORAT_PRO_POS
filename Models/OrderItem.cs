using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FFPOS.Models;

public class OrderItem : INotifyPropertyChanged
{
    private int _quantity;
    private decimal _price;

    public int Id { get; set; }
    public int OrderNumber { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = new();
    public decimal Discount { get; set; }
    public decimal Bonus { get; set; }

    public decimal Price
    {
        get => _price;
        set
        {
            if (_price == value)
            {
                return;
            }

            _price = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Total));
        }
    }

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
