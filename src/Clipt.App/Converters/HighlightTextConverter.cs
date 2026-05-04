using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Clipt.App.Converters;

public sealed class HighlightTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var text = values.ElementAtOrDefault(0) as string ?? string.Empty;
        var query = values.ElementAtOrDefault(1) as string ?? string.Empty;
        var fontSize = parameter is string sizeText && double.TryParse(sizeText, out var size) ? size : 13;

        var textBlock = new TextBlock
        {
            FontSize = fontSize,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (Brush)Application.Current.FindResource("Clipt.Brush.Text"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (string.IsNullOrWhiteSpace(query))
        {
            textBlock.Inlines.Add(new Run(text));
            return textBlock;
        }

        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            textBlock.Inlines.Add(new Run(text));
            return textBlock;
        }

        if (index > 0)
        {
            textBlock.Inlines.Add(new Run(text[..index]));
        }

        textBlock.Inlines.Add(new Run(text.Substring(index, query.Length))
        {
            Background = (Brush)Application.Current.FindResource("Clipt.Brush.Highlight"),
            Foreground = Brushes.White,
        });

        var remainderStart = index + query.Length;
        if (remainderStart < text.Length)
        {
            textBlock.Inlines.Add(new Run(text[remainderStart..]));
        }

        return textBlock;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
