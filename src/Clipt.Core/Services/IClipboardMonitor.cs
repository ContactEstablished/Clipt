using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IClipboardMonitor
{
    event EventHandler<ClipboardItem>? ClipboardItemCaptured;

    bool IsCapturing { get; }

    bool IsPaused { get; }

    event EventHandler? CaptureStateChanged;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task SetPausedAsync(bool paused, CancellationToken cancellationToken);

    /// <summary>
    /// Resets the in-memory duplicate tracking state so that text that was
    /// previously captured (and later deleted or cleared by the user) can be
    /// captured again. Does not restart the Win32 listener.
    /// </summary>
    void ResetDuplicateTracking();
}
