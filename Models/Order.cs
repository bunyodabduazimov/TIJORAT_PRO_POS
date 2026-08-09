using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FFPOS.Models;

public class Order : INotifyPropertyChanged
{
    private string _orderType = "В зале";
    private decimal _discount;
    private bool _isSelected;

    public Order()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    public int Number { get; set; }
    public string Status { get; set; } = "open";
    public ObservableCollection<OrderItem> Items { get; } = new();

    public string OrderType
    {
        get => _orderType;
        set
        {
            if (_orderType == value)
            {
                return;
            }

            _orderType = value;
            OnPropertyChanged();
        }
    }

    public decimal Discount
    {
        get => _discount;
        set
        {
            if (_discount == value)
            {
                return;
            }

            _discount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Total));
        }
    }

    public decimal Subtotal => Items.Sum(item => item.Total);
    public decimal Total => Math.Max(0, Subtotal - Discount);
    public string DisplayName => $"Чек №{Number}";

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

    public void RefreshTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Total));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (OrderItem item in e.NewItems)
            {
                item.PropertyChanged += OnItemChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (OrderItem item in e.OldItems)
            {
                item.PropertyChanged -= OnItemChanged;
            }
        }

        RefreshTotals();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OrderItem.Total) or nameof(OrderItem.Quantity))
        {
            RefreshTotals();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
