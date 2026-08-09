using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FFPOS.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public double Expanded { get; set; } = 340;
    public double Collapsed { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is bool isExpanded && isExpanded ? Expanded : Collapsed;
        return new GridLength(width);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is GridLength gridLength && gridLength.Value > Collapsed;
    }
}
