using System.Windows;
using System.Windows.Controls;

namespace Clipt.App.Controls;

public partial class MarkdownPreviewControl : UserControl
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownPreviewControl),
        new PropertyMetadata(string.Empty));

    public MarkdownPreviewControl()
    {
        InitializeComponent();
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }
}
