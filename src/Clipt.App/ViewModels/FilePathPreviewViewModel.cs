using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Clipt.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clipt.App.ViewModels;

public sealed partial class FilePathPreviewViewModel : ObservableObject
{
    private static readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger(nameof(FilePathPreviewViewModel));

    private const int CopyMaxRetries = 4;
    private static readonly int[] CopyRetryBackoffMs = [25, 75, 200, 500];

    private readonly SemaphoreSlim _copyLock = new(1, 1);
    private readonly SemaphoreSlim _revealLock = new(1, 1);

    public FilePathPreviewViewModel(string fullPath)
    {
        FullPath = fullPath;
        Name = FilePathDisplayHelper.GetFileName(fullPath);
        ParentPath = FilePathDisplayHelper.GetParentPath(fullPath);
        ExtensionLabel = FilePathDisplayHelper.GetExtensionLabel(fullPath);
        KindLabel = FilePathDisplayHelper.GetKindLabel(fullPath);
        Exists = FilePathDisplayHelper.Exists(fullPath);
        StatusLabel = Exists ? "Available" : "Missing";
    }

    public string FullPath { get; }
    public string Name { get; }
    public string ParentPath { get; }
    public string ExtensionLabel { get; }
    public string KindLabel { get; }
    public string KindGlyph => KindLabel == "File" ? "Fi" : "Fo";
    public bool Exists { get; }

    [ObservableProperty]
    private string _statusLabel;

    public bool IsMissing => !Exists;

    [RelayCommand]
    private async Task CopyPathAsync()
    {
        if (string.IsNullOrEmpty(FullPath))
        {
            return;
        }

        await _copyLock.WaitAsync();
        try
        {
            await CopyToClipboardWithRetryAsync(FullPath);
            var original = StatusLabel;
            StatusLabel = "Copied";
            await Task.Delay(1200);
            StatusLabel = original;
        }
        finally
        {
            _copyLock.Release();
        }
    }

    [RelayCommand]
    private async Task RevealInExplorerAsync()
    {
        if (!Exists)
        {
            return;
        }

        var explorerArg = FilePathDisplayHelper.GetExplorerArgument(FullPath);
        if (string.IsNullOrEmpty(explorerArg))
        {
            return;
        }

        await _revealLock.WaitAsync();
        try
        {
            var original = StatusLabel;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = explorerArg,
                    UseShellExecute = true,
                });
                StatusLabel = "Opened";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reveal path in Explorer.");
                StatusLabel = "Failed";
            }

            await Task.Delay(1200);
            StatusLabel = original;
        }
        finally
        {
            _revealLock.Release();
        }
    }

    private static async Task CopyToClipboardWithRetryAsync(string text)
    {
        for (var attempt = 0; attempt < CopyMaxRetries; attempt++)
        {
            Exception? captured = null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
            });

            if (captured is null)
            {
                _logger.LogDebug("Copied path to clipboard (attempt {Attempt}).", attempt + 1);
                return;
            }

            if (!IsClipboardBusy(captured))
            {
                _logger.LogError(captured, "Failed to copy path to clipboard on attempt {Attempt}.", attempt + 1);
                throw captured;
            }

            if (attempt == CopyMaxRetries - 1)
            {
                _logger.LogError("Clipboard busy after {MaxRetries} attempts.", CopyMaxRetries);
                throw captured;
            }

            _logger.LogDebug(
                "Clipboard busy during path copy, retrying in {BackoffMs}ms (attempt {Attempt}/{MaxRetries}).",
                CopyRetryBackoffMs[attempt],
                attempt + 1,
                CopyMaxRetries);

            await Task.Delay(CopyRetryBackoffMs[attempt]);
        }
    }

    private static bool IsClipboardBusy(Exception ex) =>
        ex is ExternalException
        && unchecked((uint)ex.HResult) == 0x800401D0;
}
