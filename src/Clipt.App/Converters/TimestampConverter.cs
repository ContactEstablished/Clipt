using System.Globalization;
using System.Windows.Data;

namespace Clipt.App.Converters;

public sealed class TimestampConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset timestamp)
        {
            return string.Empty;
        }

        var elapsed = DateTimeOffset.Now - timestamp;
        if (elapsed.TotalMinutes < 1)
        {
            return "now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{Math.Floor(elapsed.TotalMinutes)}m";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{Math.Floor(elapsed.TotalHours)}h";
        }

        return $"{Math.Floor(elapsed.TotalDays)}d";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
