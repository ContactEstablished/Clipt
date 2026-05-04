using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Clipt.App.ViewModels;

namespace Clipt.App.Converters;

public sealed class ClipboardViewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isClipboard = string.Equals(value as string, NavigationItems.Clipboard, StringComparison.Ordinal);
        if (parameter is string text && text.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            isClipboard = !isClipboard;
        }

        return isClipboard ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
