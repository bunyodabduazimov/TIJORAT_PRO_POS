using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace FFPOS.Converters;

public class SortDirectionToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ListSortDirection direction
            ? direction == ListSortDirection.Ascending ? PackIconKind.ArrowUp : PackIconKind.ArrowDown
            : PackIconKind.SwapVertical;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
