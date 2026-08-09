using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace FFPOS.Controls;

public partial class WindowControlButtons : UserControl
{
    private bool _showMaximize = true;

    public WindowControlButtons()
    {
        InitializeComponent();
    }

    public bool ShowMaximize
    {
        get => _showMaximize;
        set
        {
            _showMaximize = value;
            if (MaximizeButton is not null)
            {
                MaximizeButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                Width = value ? 116 : 78;
            }
        }
    }

    private void MinimizeClicked(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is not null)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeClicked(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        MaximizeIcon.Kind = window.WindowState == WindowState.Maximized
            ? PackIconKind.WindowRestore
            : PackIconKind.WindowMaximize;
    }

    private void CloseClicked(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }

    private void ControlMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
