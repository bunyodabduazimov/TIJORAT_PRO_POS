using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class AdminCodeWindow : Window
{
    private readonly string _adminCode;
    private int _codeLength;

    public AdminCodeWindow(string adminCode)
    {
        InitializeComponent();
        _adminCode = string.IsNullOrWhiteSpace(adminCode) ? "admin" : adminCode;
        Loaded += (_, _) => CodeBox.Focus();
    }

    private void ConfirmClicked(object sender, RoutedEventArgs e)
    {
        Confirm();
    }

    private void Confirm()
    {
        if (string.Equals(CodeBox.Password, _adminCode, StringComparison.Ordinal))
        {
            DialogResult = true;
            Close();
            return;
        }

        ErrorText.Text = "Неверный код администратора";
        CodeBox.SelectAll();
        CodeBox.Focus();
    }

    private void NumberClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: not null } button)
        {
            CodeBox.Password += button.Tag.ToString();
            ErrorText.Text = string.Empty;
        }
    }

    private void ClearClicked(object sender, RoutedEventArgs e)
    {
        CodeBox.Clear();
        ErrorText.Text = string.Empty;
        _codeLength = 0;
        CodeBox.Focus();
    }

    private void BackspaceClicked(object sender, RoutedEventArgs e)
    {
        if (CodeBox.Password.Length > 0)
        {
            CodeBox.Password = CodeBox.Password[..^1];
        }

        ErrorText.Text = string.Empty;
        _codeLength = CodeBox.Password.Length;
        CodeBox.Focus();
    }

    private void CodeChanged(object sender, RoutedEventArgs e)
    {
        if (CodeBox.Password.Length > _codeLength)
        {
            UiSoundPlayer.PlayPinClick();
        }

        _codeLength = CodeBox.Password.Length;
        ErrorText.Text = string.Empty;
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirm();
        }

        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
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
