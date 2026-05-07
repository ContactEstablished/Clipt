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
using Microsoft.Win32;

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
    public partial string AutoPruneAfterDaysText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaxClipSizeMegabytesText { get; set; } = "10";

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
        AutoPruneAfterDaysText = s.AutoPruneAfterDays is { } days
            ? days.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        MaxClipSizeMegabytesText = FormatBytesAsMegabytes(s.MaxClipboardItemBytes);
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

        int? autoPruneAfterDays;
        var autoPruneText = AutoPruneAfterDaysText.Trim();
        if (autoPruneText.Length == 0)
        {
            autoPruneAfterDays = null;
        }
        else if (int.TryParse(autoPruneText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDays)
                 && parsedDays >= 0)
        {
            autoPruneAfterDays = parsedDays == 0 ? null : parsedDays;
        }
        else
        {
            MessageBox.Show(
                "Auto-prune days must be empty (disabled) or a non-negative whole number.",
                "Clipt Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!TryParseMegabytesAsBytes(MaxClipSizeMegabytesText.Trim(), out var maxClipBytes))
        {
            MessageBox.Show(
                "Max clip size must be a positive number of megabytes (e.g. 10 or 0.5). Use 0 to disable the cap.",
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
            AutoPruneAfterDays = autoPruneAfterDays,
            MaxClipboardItemBytes = maxClipBytes,
            IgnoredAppNames = PrivacyEntryNormalizer.Normalize(SplitLines(IgnoredAppNamesText)),
            IgnoredAppPaths = PrivacyEntryNormalizer.Normalize(SplitLines(IgnoredAppPathsText)),
            IgnoredPatterns = PrivacyEntryNormalizer.Normalize(SplitLines(IgnoredPatternsText)),
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
            _mainViewModel.SetCachedAutoPruneAfterDays(normalized.AutoPruneAfterDays ?? 0);
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

    /// <summary>
    /// Resets the open-hotkey field to the project default. The new value is
    /// not persisted until the user clicks Save.
    /// </summary>
    [RelayCommand]
    private void ResetOpenHotkey()
    {
        OpenHotkeyText = DefaultOpenHotkey;
    }

    private const string DefaultOpenHotkey = "Ctrl+Shift+V";

    /// <summary>
    /// Opens an Open File dialog so the user can pick an executable to add
    /// to the Ignored app paths list. The selected path is appended to the
    /// existing textbox value; manual edits are preserved. Save still
    /// required to persist, and the entry is normalized at save time.
    /// </summary>
    [RelayCommand]
    private void BrowseForIgnoredAppPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select an application to ignore",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var picked = dialog.FileName;
        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        // Append to the existing free-text content so manual edits in the
        // textbox above the new entry are preserved verbatim.
        var current = IgnoredAppPathsText ?? string.Empty;
        if (current.Length == 0)
        {
            IgnoredAppPathsText = picked;
            return;
        }

        var separator = current.EndsWith('\n') || current.EndsWith('\r')
            ? string.Empty
            : Environment.NewLine;
        IgnoredAppPathsText = current + separator + picked;
    }

    private const long BytesPerMegabyte = 1_048_576;

    private static string FormatBytesAsMegabytes(int bytes)
    {
        if (bytes <= 0)
        {
            return "0";
        }

        var megabytes = bytes / (double)BytesPerMegabyte;
        // Trim trailing zeroes but keep precision when the number is fractional.
        return megabytes.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool TryParseMegabytesAsBytes(string text, out int bytes)
    {
        bytes = 0;
        if (text.Length == 0)
        {
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var megabytes)
            || megabytes < 0
            || double.IsNaN(megabytes)
            || double.IsInfinity(megabytes))
        {
            return false;
        }

        var raw = megabytes * BytesPerMegabyte;
        if (raw > int.MaxValue)
        {
            return false;
        }

        bytes = (int)Math.Round(raw);
        return true;
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
