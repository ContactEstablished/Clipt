namespace Clipt.Core.Models;

public sealed record AppSettings
{
    public string Theme { get; init; } = ThemeNames.Dark;

    public string AccentColor { get; init; } = "#14B8A6";

    public int CaptureModeWidth { get; init; } = 380;

    public int WorkModeWidth { get; init; } = 880;

    public bool StartInWorkMode { get; init; }
}
