using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Clipt.Core.Models;

namespace Clipt.App.Converters;

public sealed class ContentTypeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var data = value switch
        {
            ContentType.Markdown => "M4 4h16v16H4z M8 8l2.5 4L13 8v8 M15 8h2.5a2.5 2.5 0 0 1 0 5H15z",
            ContentType.Code => "M8 16l-4-4 4-4 M16 8l4 4-4 4 M14 4l-4 16",
            ContentType.Json => "M8 4H6a2 2 0 0 0-2 2v3a2 2 0 0 1-2 2 2 2 0 0 1 2 2v3a2 2 0 0 0 2 2h2 M16 4h2a2 2 0 0 1 2 2v3a2 2 0 0 0 2 2 2 2 0 0 0-2 2v3a2 2 0 0 1-2 2h-2",
            ContentType.Url => "M10 13a5 5 0 0 0 7.07 0l2-2a5 5 0 0 0-7.07-7.07l-1 1 M14 11a5 5 0 0 0-7.07 0l-2 2a5 5 0 0 0 7.07 7.07l1-1",
            ContentType.Image => "M4 5h16v14H4z M8 13l2.5-3 3 4 2-2.5L20 17 M8 8h.01",
            ContentType.File => "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6",
            ContentType.Color => "M12 22a7 7 0 0 0 7-7c0-5-7-13-7-13S5 10 5 15a7 7 0 0 0 7 7z",
            _ => "M4 4h16v16H4z M8 8h8 M8 12h8 M8 16h5",
        };

        return Geometry.Parse(data);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
