using System;
using System.Globalization;
using System.Windows.Data;

namespace Clipt.App.Converters;

public sealed class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is true;
        if (parameter is string text)
        {
            var parts = text.Split('|', 2);
            if (parts.Length == 2)
            {
                return isTrue ? parts[0] : parts[1];
            }
        }

        return isTrue ? "True" : "False";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
