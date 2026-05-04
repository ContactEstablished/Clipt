using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Clipt.App.Converters;

public sealed class HexColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return Brushes.Transparent;
        }

        try
        {
            var brush = new BrushConverter().ConvertFromString(text);
            return brush is SolidColorBrush solidColorBrush ? solidColorBrush : Brushes.Transparent;
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
