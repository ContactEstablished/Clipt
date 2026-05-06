using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Clipt.App.Services;
using Clipt.Core.Models;
using Clipt.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clipt.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly MainViewModel _mainViewModel;
    private readonly Action<AppSettings> _applySettingsToShell;
    private readonly DemoContentSeeder _demoContentSeeder;

    private AppSettings _baseline = new();

    public SettingsViewModel(
        ISettingsService settingsService,
        IHotkeyService hotkeyService,
        IClipboardMonitor clipboardMonitor,
        MainViewModel mainViewModel,
        Action<AppSettings> applySettingsToShell,
        DemoContentSeeder demoContentSeeder)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _clipboardMonitor = clipboardMonitor;
        _mainViewModel = mainViewModel;
        _applySettingsToShell = applySettingsToShell;
        _demoContentSeeder = demoContentSeeder;
    }

    public event EventHandler? CloseRequested;

    [ObservableProperty]
    public partial bool AutoPasteOnEnter { get; set; }

    /// <summary>Displayed disabled — restore-after-paste is not implemented yet.</summary>
    [ObservableProperty]
    public partial bool RestorePreviousClipboardAfterPaste { get; set; }

    [ObservableProperty]
    public partial string MaxHistoryItemsText { get; set; } = "500";

    [ObservableProperty]
    public partial string IgnoredAppNamesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string IgnoredAppPathsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string IgnoredPatternsText { get; set; } = string.Empty;

    /// <summary>Displayed disabled — CF viewer ignore is not wired yet.</summary>
    [ObservableProperty]
    public partial bool HonorClipboardViewerIgnore { get; set; }

    [ObservableProperty]
    public partial string OpenHotkeyText { get; set; } = string.Empty;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _baseline = await _settingsService.GetAsync(cancellationToken);
        var s = _baseline.Normalize();

        AutoPasteOnEnter = s.AutoPasteOnEnter;
        RestorePreviousClipboardAfterPaste = s.RestorePreviousClipboardAfterPaste;
        MaxHistoryItemsText = s.MaxHistoryItems.ToString(CultureInfo.InvariantCulture);
        IgnoredAppNamesText = JoinLines(s.IgnoredAppNames);
        IgnoredAppPathsText = JoinLines(s.IgnoredAppPaths);
        IgnoredPatternsText = JoinLines(s.IgnoredPatterns);
        HonorClipboardViewerIgnore = s.HonorClipboardViewerIgnore;
        OpenHotkeyText = s.OpenHotkey;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var hotkey = OpenHotkeyText.Trim();
        if (HotkeyGestureParser.Parse(hotkey) is null)
        {
            MessageBox.Show(
                "Enter a valid hotkey such as Ctrl+Shift+V (modifiers plus one key).",
                "Clipt Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(MaxHistoryItemsText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxHistory)
            || maxHistory < 0)
        {
            MessageBox.Show(
                "Max history items must be zero or a positive whole number.",
                "Clipt Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var updated = _baseline with
        {
            AutoPasteOnEnter = AutoPasteOnEnter,
            RestorePreviousClipboardAfterPaste = _baseline.RestorePreviousClipboardAfterPaste,
            MaxHistoryItems = maxHistory,
            IgnoredAppNames = SplitLines(IgnoredAppNamesText),
            IgnoredAppPaths = SplitLines(IgnoredAppPathsText),
            IgnoredPatterns = SplitLines(IgnoredPatternsText),
            HonorClipboardViewerIgnore = _baseline.HonorClipboardViewerIgnore,
            OpenHotkey = hotkey,
        };

        var normalized = updated.Normalize();

        try
        {
            await _settingsService.SaveAsync(normalized, CancellationToken.None);
            _baseline = normalized;
            _applySettingsToShell(normalized);
            _mainViewModel.SetCachedMaxHistoryItems(normalized.MaxHistoryItems);
            await _clipboardMonitor.RefreshCachedPrivacySettingsAsync(CancellationToken.None);
            await _hotkeyService.RegisterFromSettingsAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Could not save settings. Please try again.",
                "Clipt Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task LoadDemoContentAsync()
    {
        var confirmed = MessageBox.Show(
            "Add curated demo clips to your history?\n\nYour existing items will not be affected. Duplicates are skipped.",
            "Clipt Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmed != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = await _demoContentSeeder.SeedAsync(CancellationToken.None);

            var message = result.Inserted > 0
                ? $"Added {result.Inserted} demo clip{(result.Inserted == 1 ? "" : "s")}."
                : "All demo clips are already in your history.";

            if (result.Updated > 0)
            {
                message += $"\n{result.Updated} clip{(result.Updated == 1 ? "" : "s")} already existed and {(result.Updated == 1 ? "was" : "were")} skipped.";
            }

            await _mainViewModel.RefreshFromDatabaseAsync();

            MessageBox.Show(
                message,
                "Clipt Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Could not load demo content. Please try again.",
                "Clipt Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string JoinLines(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static List<string> SplitLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .ToList();
    }
}
