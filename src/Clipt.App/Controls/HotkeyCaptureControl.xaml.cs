using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clipt.App.Controls;

public partial class HotkeyCaptureControl : UserControl
{
    public static readonly DependencyProperty HotkeyTextProperty =
        DependencyProperty.Register(
            nameof(HotkeyText),
            typeof(string),
            typeof(HotkeyCaptureControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHotkeyTextChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(
            nameof(DisplayText),
            typeof(string),
            typeof(HotkeyCaptureControl),
            new PropertyMetadata("Click to record"));

    public static readonly DependencyProperty StateLabelProperty =
        DependencyProperty.Register(
            nameof(StateLabel),
            typeof(string),
            typeof(HotkeyCaptureControl),
            new PropertyMetadata("Record"));

    public static readonly DependencyProperty IsCapturingProperty =
        DependencyProperty.Register(
            nameof(IsCapturing),
            typeof(bool),
            typeof(HotkeyCaptureControl),
            new PropertyMetadata(false, OnIsCapturingChanged));

    public HotkeyCaptureControl()
    {
        InitializeComponent();
        RefreshDisplay();
    }

    public string HotkeyText
    {
        get => (string)GetValue(HotkeyTextProperty);
        set => SetValue(HotkeyTextProperty, value);
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        private set => SetValue(DisplayTextProperty, value);
    }

    public string StateLabel
    {
        get => (string)GetValue(StateLabelProperty);
        private set => SetValue(StateLabelProperty, value);
    }

    public bool IsCapturing
    {
        get => (bool)GetValue(IsCapturingProperty);
        private set => SetValue(IsCapturingProperty, value);
    }

    private static void OnHotkeyTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyCaptureControl control)
        {
            control.RefreshDisplay();
        }
    }

    private static void OnIsCapturingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyCaptureControl control)
        {
            control.RefreshDisplay();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginCapture();
        e.Handled = true;
    }

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        IsCapturing = false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsCapturing)
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                BeginCapture();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Escape)
        {
            IsCapturing = false;
            e.Handled = true;
            return;
        }

        var key = ResolveKey(e);
        if (key == Key.None || IsModifierKey(key))
        {
            // Live preview: show the modifiers currently held while the user
            // is still composing the shortcut (before pressing the final key).
            RenderCapturePreview();
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            DisplayText = "Add Ctrl, Alt, Shift, or Win";
            StateLabel = "Waiting";
            e.Handled = true;
            return;
        }

        HotkeyText = FormatHotkey(modifiers, key);
        IsCapturing = false;
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!IsCapturing)
        {
            return;
        }

        // When a modifier is released mid-capture, refresh the preview so the
        // user always sees what is currently held.
        var key = ResolveKey(e);
        if (IsModifierKey(key))
        {
            RenderCapturePreview();
        }
    }

    private void BeginCapture()
    {
        IsCapturing = true;
        Focus();
        Keyboard.Focus(this);
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (IsCapturing)
        {
            RenderCapturePreview();
            return;
        }

        DisplayText = FormatDisplayText(HotkeyText);
        StateLabel = "Record";
    }

    /// <summary>
    /// Renders the current capture preview. If no modifiers are held, shows
    /// the generic "Press a shortcut" prompt; otherwise echoes the held
    /// modifiers with a trailing ellipsis (e.g. "Ctrl + Shift + …") so the
    /// user has feedback while composing the gesture.
    /// </summary>
    private void RenderCapturePreview()
    {
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            DisplayText = "Press a shortcut";
            StateLabel = "Listening";
            return;
        }

        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add("…");
        DisplayText = string.Join(" + ", parts);
        StateLabel = "Listening";
    }

    private static Key ResolveKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key == Key.ImeProcessed ? e.ImeProcessedKey : key;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin;
    }

    private static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private static string FormatDisplayText(string? hotkeyText)
    {
        if (string.IsNullOrWhiteSpace(hotkeyText))
        {
            return "Click to record";
        }

        return string.Join(" + ", hotkeyText
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(FormatKeyToken));
    }

    private static string FormatKeyToken(string token)
    {
        if (token.Length == 2
            && token[0] == 'D'
            && char.IsDigit(token[1]))
        {
            return token[1].ToString();
        }

        return token switch
        {
            "OemPlus" => "+",
            "OemMinus" => "-",
            "OemComma" => ",",
            "OemPeriod" => ".",
            "OemQuestion" => "/",
            "OemSemicolon" => ";",
            "OemQuotes" => "'",
            "OemOpenBrackets" => "[",
            "OemCloseBrackets" => "]",
            "OemPipe" => "\\",
            "Space" => "Space",
            _ => token,
        };
    }
}
