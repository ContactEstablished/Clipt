using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Clipt.Core.Models;

namespace Clipt.App.Converters;

public sealed class ContentTypeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ContentType contentType || parameter is not string expected)
        {
            return Visibility.Collapsed;
        }

        var visible = expected.Split('|', StringSplitOptions.TrimEntries)
            .Any(candidate => Enum.TryParse<ContentType>(candidate, out var parsed) && parsed == contentType);

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
