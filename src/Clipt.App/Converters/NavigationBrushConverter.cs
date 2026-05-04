using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Clipt.App.Converters;

public sealed class NavigationBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var active = value as string;
        var candidate = parameter as string;
        var resourceKey = string.Equals(active, candidate, StringComparison.Ordinal)
            ? "Clipt.Brush.Accent"
            : "Clipt.Brush.TextMuted";

        return (Brush)Application.Current.FindResource(resourceKey);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
