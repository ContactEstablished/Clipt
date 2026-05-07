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
        new PropertyMetadata(null, OnImageSourceChanged));

    public static readonly DependencyProperty IsImageLoadedProperty = DependencyProperty.Register(
        nameof(IsImageLoaded),
        typeof(bool),
        typeof(ImagePreviewControl),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ImageDetailTextProperty = DependencyProperty.Register(
        nameof(ImageDetailText),
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

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        private set => SetValue(ImageSourceProperty, value);
    }

    public bool IsImageLoaded
    {
        get => (bool)GetValue(IsImageLoadedProperty);
        private set => SetValue(IsImageLoadedProperty, value);
    }

    public string ImageDetailText
    {
        get => (string)GetValue(ImageDetailTextProperty);
        set => SetValue(ImageDetailTextProperty, value);
    }

    private static void OnImageUriChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ImagePreviewControl control)
        {
            return;
        }

        var imageUri = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(imageUri))
        {
            control.UpdateImageState(null);
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
            control.UpdateImageState(null);
        }
    }

    private static void OnImageSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ImagePreviewControl control)
        {
            control.IsImageLoaded = e.NewValue is not null;
        }
    }

    private void UpdateImageState(ImageSource? source)
    {
        ImageSource = source;
        IsImageLoaded = source is not null;
    }
}
