using System.Windows.Input;
using System.Windows.Interop;
using Clipt.Core.Services;
using Microsoft.Extensions.Logging;
using NHotkey;
using NHotkey.Wpf;

namespace Clipt.App.Services;

public sealed class GlobalHotkeyService : IHotkeyService, IDisposable
{
    private const string HotkeyName = "OpenClipt";

    private readonly ILogger<GlobalHotkeyService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly ForegroundWindowTracker _foregroundTracker;
    private bool _isRegistered;

    public GlobalHotkeyService(
        ISettingsService settingsService,
        ForegroundWindowTracker foregroundTracker,
        ILogger<GlobalHotkeyService> logger)
    {
        _settingsService = settingsService;
        _foregroundTracker = foregroundTracker;
        _logger = logger;
    }

    public event EventHandler? HotkeyPressed;

    public async Task RegisterFromSettingsAsync(CancellationToken cancellationToken)
    {
        Unregister();

        var settings = await _settingsService.GetAsync(cancellationToken);
        var hotkeyText = settings.OpenHotkey;

        if (string.IsNullOrWhiteSpace(hotkeyText))
        {
            _logger.LogDebug("No OpenHotkey configured; skipping global hotkey registration.");
            return;
        }

        var parsed = HotkeyGestureParser.Parse(hotkeyText);
        if (parsed is null)
        {
            _logger.LogWarning("Failed to parse OpenHotkey '{Hotkey}'.", hotkeyText);
            return;
        }

        if (!TryConvertToWpf(parsed, out var key, out var modifiers))
        {
            _logger.LogWarning("OpenHotkey '{Hotkey}' parsed but could not be mapped to WPF key/modifier.", hotkeyText);
            return;
        }

        try
        {
            HotkeyManager.Current.AddOrReplace(HotkeyName, key, modifiers, OnHotkeyFired);
            _isRegistered = true;
            _logger.LogInformation("Registered global hotkey: {Hotkey}", hotkeyText);
        }
        catch (HotkeyAlreadyRegisteredException)
        {
            _logger.LogWarning(
                "Global hotkey '{Hotkey}' is already registered by another application.", hotkeyText);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to register global hotkey '{Hotkey}'.", hotkeyText);
        }
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        try
        {
            HotkeyManager.Current.Remove(HotkeyName);
            _logger.LogInformation("Unregistered global hotkey.");
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to unregister global hotkey (may already be removed).");
        }

        _isRegistered = false;
    }

    public void Dispose()
    {
        Unregister();
    }

    private void OnHotkeyFired(object? sender, HotkeyEventArgs e)
    {
        e.Handled = true;
        _foregroundTracker.Capture();
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryConvertToWpf(
        Clipt.Core.Models.ParsedHotkey parsed,
        out Key key,
        out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;

        if (!Enum.TryParse(parsed.Key, ignoreCase: true, out key))
        {
            return false;
        }

        foreach (var mod in parsed.Modifiers)
        {
            modifiers |= mod switch
            {
                "Ctrl" => ModifierKeys.Control,
                "Alt" => ModifierKeys.Alt,
                "Shift" => ModifierKeys.Shift,
                "Win" => ModifierKeys.Windows,
                _ => ModifierKeys.None,
            };
        }

        return true;
    }
}
