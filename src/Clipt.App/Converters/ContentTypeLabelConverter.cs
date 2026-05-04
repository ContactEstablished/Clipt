using System.Globalization;
using System.Windows.Data;
using Clipt.Core.Models;

namespace Clipt.App.Converters;

public sealed class ContentTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ContentType.Json => "JSON",
            ContentType.Url => "URL",
            ContentType.File => "Files",
            ContentType.Text => "Plain text",
            ContentType.Code => "Code",
            ContentType.Markdown => "Markdown",
            ContentType.Image => "Image",
            ContentType.Color => "Color",
            _ => "Clip",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
