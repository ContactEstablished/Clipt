namespace Clipt.App.Services;

/// <summary>
/// Manages the global open/summon hotkey. Register once from settings on
/// startup; unregister on shutdown.
/// </summary>
public interface IHotkeyService
{
    /// <summary>Raised when the registered global hotkey is pressed.</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Reads <c>AppSettings.OpenHotkey</c>, parses it, and registers the
    /// global hotkey. Safe to call when already registered: re-registers.
    /// </summary>
    Task RegisterFromSettingsAsync(CancellationToken cancellationToken);

    /// <summary>Removes the global hotkey registration.</summary>
    void Unregister();
}
