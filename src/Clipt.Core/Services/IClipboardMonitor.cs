using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IClipboardMonitor
{
    event EventHandler<ClipboardItem>? ClipboardItemCaptured;

    bool IsCapturing { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
