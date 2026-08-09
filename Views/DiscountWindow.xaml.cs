using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace FFPOS.Views;

public partial class DiscountWindow : Window
{
    private readonly decimal _subtotal;

    public DiscountWindow(decimal subtotal, decimal currentDiscount)
    {
        InitializeComponent();
        _subtotal = subtotal;
        AmountBox.Text = currentDiscount.ToString("0", CultureInfo.CurrentCulture);
    }

    public decimal Discount { get; private set; }

    private void Apply_OnClick(object sender, RoutedEventArgs e)
    {
        _ = decimal.TryParse(PercentBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var percent);
        _ = decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount);

        if (percent > 0)
        {
            amount = _subtotal * Math.Clamp(percent, 0, 100) / 100;
        }

        Discount = Math.Clamp(amount, 0, _subtotal);
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
