using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clipt.App.Controls;

public partial class ImagePreviewControl : UserControl
{
    public static readonly DependencyProperty ImageUriProperty = DependencyProperty.Register(
        nameof(ImageUri),
        typeof(string),
        typeof(ImagePreviewControl),
        new PropertyMetadata(string.Empty, OnImageUriChanged));

    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource),
        typeof(ImageSource),
        typeof(ImagePreviewControl),
        new PropertyMetadata(null));

    public ImagePreviewControl()
    {
        InitializeComponent();
    }

    public string ImageUri
    {
        get => (string)GetValue(ImageUriProperty);
        set => SetValue(ImageUriProperty, value);
    }

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        private set => SetValue(ImageSourceProperty, value);
    }

    private static void OnImageUriChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ImagePreviewControl control || e.NewValue is not string imageUri)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(imageUri))
        {
            control.ImageSource = null;
            return;
        }

        try
        {
            var uri = new Uri(imageUri, UriKind.RelativeOrAbsolute);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            control.ImageSource = bitmap;
        }
        catch
        {
            control.ImageSource = null;
        }
    }
}
