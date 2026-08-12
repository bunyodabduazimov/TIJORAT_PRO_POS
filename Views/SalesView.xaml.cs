using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FFPOS.Controls;
using FFPOS.Models;
using FFPOS.ViewModels;

namespace FFPOS.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        var viewModel = new SalesViewModel();
        viewModel.OrderItemTouched += OnOrderItemTouched;
        DataContext = viewModel;
    }

    private void OnOrderItemTouched(object? sender, OrderItem item)
    {
        Dispatcher.BeginInvoke(() => ScrollToOrderItem(item), DispatcherPriority.Loaded);
    }

    private void ScrollToOrderItem(OrderItem item)
    {
        ReceiptItemsControl.UpdateLayout();

        var container = ReceiptItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
        if (container is null)
        {
            ReceiptItemsScrollViewer.ScrollToEnd();
            ReceiptItemsControl.UpdateLayout();
            container = ReceiptItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
        }

        if (container is null)
        {
            return;
        }

        container.Dispatcher.BeginInvoke(() =>
        {
            if (FindVisualChild<OrderItemRow>(container) is not { } row)
            {
                return;
            }

            row.BringIntoView();
            row.FlashHighlight();
        }, DispatcherPriority.Loaded);
    }

    private void ProfileButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = !ProfilePopup.IsOpen;
    }

    private void OrderActionsButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        OrderActionsPopup.IsOpen = !OrderActionsPopup.IsOpen;
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Maximized)
        {
            return;
        }

        window.DragMove();
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindVisualChild<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(source); i++)
        {
            var child = VisualTreeHelper.GetChild(source, i);
            if (child is T result)
            {
                return result;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
