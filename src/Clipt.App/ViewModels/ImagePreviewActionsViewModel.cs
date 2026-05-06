using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Clipt.App.Helpers;
using Clipt.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clipt.App.ViewModels;

public sealed partial class ImagePreviewActionsViewModel : ObservableObject
{
    private static readonly ILogger _logger =
        NullLoggerFactory.Instance.CreateLogger(nameof(ImagePreviewActionsViewModel));

    private readonly SemaphoreSlim _copyLock = new(1, 1);
    private readonly SemaphoreSlim _revealLock = new(1, 1);

    public ImagePreviewActionsViewModel(string? imageUri)
    {
        ImagePreviewLocalPath = FilePathDisplayHelper.ConvertFileUriToLocalPath(imageUri);
        HasPreviewPath = ImagePreviewLocalPath is not null && File.Exists(ImagePreviewLocalPath);
        StatusLabel = HasPreviewPath ? "Cached" : "Missing";
    }

    public string? ImagePreviewLocalPath { get; }
    public bool HasPreviewPath { get; }

    [ObservableProperty]
    private string _statusLabel = "Missing";

    [RelayCommand]
    private async Task CopyImagePreviewPathAsync()
    {
        if (string.IsNullOrEmpty(ImagePreviewLocalPath))
        {
            return;
        }

        await _copyLock.WaitAsync();
        try
        {
            await ClipboardCopyHelper.CopyTextWithRetryAsync(
                ImagePreviewLocalPath, "image preview path");
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
    private async Task RevealImagePreviewAsync()
    {
        if (!HasPreviewPath || string.IsNullOrEmpty(ImagePreviewLocalPath))
        {
            return;
        }

        var explorerArg = FilePathDisplayHelper.GetExplorerArgument(ImagePreviewLocalPath);
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
                _logger.LogError(ex, "Failed to reveal image preview in Explorer.");
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
}
