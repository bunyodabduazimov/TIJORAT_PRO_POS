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
    public int StoreId { get; set; } = 1;
    public int StockId { get; set; } = 1;
    public int UserId { get; set; } = 1;
    public int CashId { get; set; } = 1;
    public int PriceId { get; set; } = 1;
    public int PeopleId { get; set; } = 1;
    public decimal Summa { get; set; }
    public decimal BonusSum { get; set; }
    public decimal SummaPay { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string Status { get; set; } = "open";
    public int SyncStatus { get; set; }
    public int? ServerId { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? SyncError { get; set; }
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
