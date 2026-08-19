using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace FFPOS.Views;

public partial class OrderValueInputWindow : Window
{
    private readonly bool _allowDecimal;
    private bool _replaceOnNextInput = true;

    public decimal Value { get; private set; }

    public OrderValueInputWindow(string title, string initialValue, bool allowDecimal = true)
    {
        InitializeComponent();

        TitleText.Text = title;
        _allowDecimal = allowDecimal;
        DotButton.Visibility = allowDecimal ? Visibility.Visible : Visibility.Collapsed;
        ValueBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
            _replaceOnNextInput = true;
        };
    }

    private void KeypadClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is null)
        {
            return;
        }

        var key = element.Tag.ToString() ?? string.Empty;
        if (key == "." && !_allowDecimal)
        {
            return;
        }

        if (key == "." && ValueBox.Text.Contains('.'))
        {
            return;
        }

        if (_replaceOnNextInput || string.IsNullOrWhiteSpace(ValueBox.Text) || ValueBox.Text == "0")
        {
            ValueBox.Text = key == "." ? "0." : key;
            _replaceOnNextInput = false;
            ValueBox.CaretIndex = ValueBox.Text.Length;
            return;
        }

        if (key == "." && string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            ValueBox.Text = "0.";
            _replaceOnNextInput = false;
            ValueBox.CaretIndex = ValueBox.Text.Length;
            return;
        }

        ValueBox.Text += key;
        _replaceOnNextInput = false;
        ValueBox.CaretIndex = ValueBox.Text.Length;
    }

    private void BackspaceClicked(object sender, RoutedEventArgs e)
    {
        if (ValueBox.Text.Length == 0)
        {
            _replaceOnNextInput = true;
            return;
        }

        ValueBox.Text = ValueBox.Text[..^1];
        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            ValueBox.Text = "0";
            _replaceOnNextInput = true;
        }

        ValueBox.CaretIndex = ValueBox.Text.Length;
    }

    private void ValueBoxTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_allowDecimal && ValueBox.Text.Contains('.'))
        {
            ValueBox.Text = ValueBox.Text.Replace(".", string.Empty);
            ValueBox.CaretIndex = ValueBox.Text.Length;
        }

        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            ValueBox.Text = "0";
            ValueBox.CaretIndex = ValueBox.Text.Length;
            _replaceOnNextInput = true;
        }
    }

    private void OkClicked(object sender, RoutedEventArgs e)
    {
        if (!TryParseValue(ValueBox.Text, out var value))
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
            return;
        }

        Value = value;
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

    private static bool TryParseValue(string text, out decimal value)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            value = 0;
            return false;
        }

        var normalized = trimmed.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }
}
