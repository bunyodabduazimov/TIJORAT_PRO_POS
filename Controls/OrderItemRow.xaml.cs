using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FFPOS.Models;

namespace FFPOS.Controls;

public partial class OrderItemRow : UserControl
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(nameof(Item), typeof(OrderItem), typeof(OrderItemRow), new PropertyMetadata(null));

    public static readonly DependencyProperty IncreaseCommandProperty =
        DependencyProperty.Register(nameof(IncreaseCommand), typeof(ICommand), typeof(OrderItemRow), new PropertyMetadata(null));

    public static readonly DependencyProperty DecreaseCommandProperty =
        DependencyProperty.Register(nameof(DecreaseCommand), typeof(ICommand), typeof(OrderItemRow), new PropertyMetadata(null));

    public static readonly DependencyProperty RemoveCommandProperty =
        DependencyProperty.Register(nameof(RemoveCommand), typeof(ICommand), typeof(OrderItemRow), new PropertyMetadata(null));

    public OrderItem? Item
    {
        get => (OrderItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public ICommand? IncreaseCommand
    {
        get => (ICommand?)GetValue(IncreaseCommandProperty);
        set => SetValue(IncreaseCommandProperty, value);
    }

    public ICommand? DecreaseCommand
    {
        get => (ICommand?)GetValue(DecreaseCommandProperty);
        set => SetValue(DecreaseCommandProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public OrderItemRow()
    {
        InitializeComponent();
    }
}
