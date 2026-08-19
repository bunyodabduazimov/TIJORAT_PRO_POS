using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace FFPOS.Views;

public partial class AppDialogWindow : Window
{
    private AppDialogWindow(
        string title,
        string message,
        bool isConfirm,
        PackIconKind icon,
        Color accentColor,
        Color accentBackground,
        string confirmText = "Да",
        string cancelText = "Отмена")
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        DialogIcon.Kind = icon;
        DialogIcon.Foreground = new SolidColorBrush(accentColor);
        IconCircle.Background = new SolidColorBrush(accentBackground);

        CancelButton.Visibility = isConfirm ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Content = cancelText;
        OkButton.Content = isConfirm ? confirmText : "OK";
        OkButton.Width = isConfirm ? 160 : 180;
        OkButton.HorizontalAlignment = HorizontalAlignment.Center;

        if (!isConfirm)
        {
            Grid.SetColumn(OkButton, 0);
            Grid.SetColumnSpan(OkButton, 2);
            OkButton.Margin = new Thickness(0);
        }
    }

    public static bool ShowInfo(string message, string title = "Сообщение", Window? owner = null)
    {
        return Show(title, message, false, PackIconKind.InformationOutline, Color.FromRgb(37, 99, 235), Color.FromRgb(239, 246, 255), owner);
    }

    public static bool ShowSuccess(string message, string title = "Успешно", Window? owner = null)
    {
        return Show(title, message, false, PackIconKind.CheckCircleOutline, Color.FromRgb(22, 163, 74), Color.FromRgb(240, 253, 244), owner);
    }

    public static bool ShowError(string message, string title = "Ошибка", Window? owner = null)
    {
        return Show(title, message, false, PackIconKind.AlertCircleOutline, Color.FromRgb(249, 31, 37), Color.FromRgb(255, 240, 239), owner);
    }

    public static bool Confirm(string message, string title = "Подтверждение", Window? owner = null)
    {
        return Show(title, message, true, PackIconKind.HelpCircleOutline, Color.FromRgb(249, 31, 37), Color.FromRgb(255, 240, 239), owner);
    }

    public static bool Confirm(
        string message,
        string title,
        string confirmText,
        string cancelText,
        Window? owner = null)
    {
        return Show(title, message, true, PackIconKind.HelpCircleOutline, Color.FromRgb(249, 31, 37), Color.FromRgb(255, 240, 239), owner, confirmText, cancelText);
    }

    private static bool Show(
        string title,
        string message,
        bool isConfirm,
        PackIconKind icon,
        Color accentColor,
        Color accentBackground,
        Window? owner,
        string confirmText = "Да",
        string cancelText = "Отмена")
    {
        var window = new AppDialogWindow(title, message, isConfirm, icon, accentColor, accentBackground, confirmText, cancelText)
        {
            Owner = owner ?? Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    private void OkClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
