using System.Windows;
using System.Windows.Controls;

namespace Clipt.App.Controls;

public partial class ImagePreviewControl : UserControl
{
    public static readonly DependencyProperty ImageUriProperty = DependencyProperty.Register(
        nameof(ImageUri),
        typeof(string),
        typeof(ImagePreviewControl),
        new PropertyMetadata(string.Empty));

    public ImagePreviewControl()
    {
        InitializeComponent();
    }

    public string ImageUri
    {
        get => (string)GetValue(ImageUriProperty);
        set => SetValue(ImageUriProperty, value);
    }
}
