using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
        ReceiptItemsGrid.ScrollIntoView(item);
        ReceiptItemsGrid.UpdateLayout();

        var container = ReceiptItemsGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
        if (container is null)
        {
            ReceiptItemsGrid.ScrollIntoView(item);
            ReceiptItemsGrid.UpdateLayout();
            container = ReceiptItemsGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
        }

        if (container is null)
        {
            return;
        }

        container.Dispatcher.BeginInvoke(() =>
        {
            container.BringIntoView();
            FlashHighlight(container);
        }, DispatcherPriority.Loaded);
    }

    private void ReceiptItemCell_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridCell cell || cell.DataContext is not OrderItem item || DataContext is not SalesViewModel viewModel)
        {
            return;
        }

        if (!viewModel.IsTouchScreen)
        {
            return;
        }

        switch (cell.Tag as string)
        {
            case "Quantity":
                viewModel.EditCurrentOrderQuantityCommand.Execute(item);
                e.Handled = true;
                break;
            case "Price":
                viewModel.EditCurrentOrderPriceCommand.Execute(item);
                e.Handled = true;
                break;
            case "Total":
                viewModel.EditCurrentOrderTotalCommand.Execute(item);
                e.Handled = true;
                break;
        }
    }

    private void CurrentOrderInlineValue_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox ||
            textBox.DataContext is not OrderItem item ||
            DataContext is not SalesViewModel viewModel ||
            textBox.Tag is not string field)
        {
            return;
        }

        viewModel.ApplyCurrentOrderInlineEdit(item, field, textBox.Text);
    }

    private void SelectedOrderInlineValue_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox ||
            textBox.DataContext is not OrderItem item ||
            DataContext is not SalesViewModel viewModel ||
            textBox.Tag is not string field)
        {
            return;
        }

        viewModel.ApplySelectedOrderInlineEdit(item, field, textBox.Text);
    }

    private void SelectedOrderInlineValue_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox ||
            textBox.DataContext is not OrderItem item ||
            DataContext is not SalesViewModel viewModel ||
            textBox.Tag is not string field ||
            !viewModel.IsTouchScreen)
        {
            return;
        }

        switch (field)
        {
            case "Quantity":
                viewModel.EditSelectedOrderQuantityCommand.Execute(item);
                e.Handled = true;
                break;
            case "Price":
                viewModel.EditSelectedOrderPriceCommand.Execute(item);
                e.Handled = true;
                break;
            case "Total":
                viewModel.EditSelectedOrderTotalCommand.Execute(item);
                e.Handled = true;
                break;
        }
    }

    private void InlineValue_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch) && ch != ',' && ch != '.');
    }

    private void ProfileButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = !ProfilePopup.IsOpen;
    }

    private void OrderActionsButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        OrderActionsPopup.IsOpen = !OrderActionsPopup.IsOpen;
    }

    private void PaymentActionsButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        PaymentActionsPopup.IsOpen = !PaymentActionsPopup.IsOpen;
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

    private static void FlashHighlight(DataGridRow row)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF0EF"));
        row.Background = brush;

        var animation = new ColorAnimation
        {
            From = (Color)ColorConverter.ConvertFromString("#FFF0EF"),
            To = Colors.White,
            Duration = TimeSpan.FromMilliseconds(900),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }
}
