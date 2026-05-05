namespace Clipt.Core.Models;

public sealed record AppSettings
{
    public bool IsWorkMode { get; init; }

    public double Opacity { get; init; } = 1.0;

    public int CaptureModeWidth { get; init; } = 380;

    public int WorkModeWidth { get; init; } = 880;

    public int Height { get; init; } = 640;

    public int? Left { get; init; }

    public int? Top { get; init; }

    public bool AlwaysOnTop { get; init; }

    public string OpenHotkey { get; init; } = "Ctrl+Shift+V";

    public string Theme { get; init; } = ThemeNames.Dark;

    public string AccentColor { get; init; } = "#14B8A6";

    public bool AutoPasteOnEnter { get; init; } = true;

    public bool RestorePreviousClipboardAfterPaste { get; init; } = false;

    public AppSettings ClampOpacity()
    {
        if (Opacity >= 0.1 && Opacity <= 1.0)
        {
            return this;
        }

        return this with { Opacity = Math.Clamp(Opacity, 0.1, 1.0) };
    }

    public AppSettings Normalize()
    {
        var settings = this with { Opacity = Math.Round(Opacity, 2) };
        return settings.ClampOpacity();
    }
}
