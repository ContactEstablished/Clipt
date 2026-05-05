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
}
