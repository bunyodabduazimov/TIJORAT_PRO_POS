using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.ViewModels;

namespace FFPOS.Views;

public partial class SalesFastFoodView : UserControl
{
    public SalesFastFoodView()
    {
        InitializeComponent();
        DataContext = new SalesViewModel();
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
            window.WindowState = WindowState.Normal;
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
}
