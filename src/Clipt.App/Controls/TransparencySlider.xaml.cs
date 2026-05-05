using System.Windows;
using System.Windows.Controls;

namespace Clipt.App.Controls;

public partial class TransparencySlider : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(TransparencySlider),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<double>? ValueChanged;

    public TransparencySlider()
    {
        InitializeComponent();
        InternalSlider.ValueChanged += (s, e) => ValueChanged?.Invoke(this, e);
    }
}
