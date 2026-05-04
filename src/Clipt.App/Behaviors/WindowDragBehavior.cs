using System.Windows;
using System.Windows.Input;

namespace Clipt.App.Behaviors;

public static class WindowDragBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(WindowDragBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if (e.NewValue is true)
        {
            element.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }
        else
        {
            element.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        }
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element
            && Window.GetWindow(element) is { } window
            && e.ButtonState == MouseButtonState.Pressed)
        {
            window.DragMove();
        }
    }
}
