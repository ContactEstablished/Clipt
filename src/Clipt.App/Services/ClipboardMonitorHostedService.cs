using Clipt.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

public sealed class ClipboardMonitorHostedService(
    IClipboardMonitor clipboardMonitor,
    ILogger<ClipboardMonitorHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await clipboardMonitor.StartAsync(cancellationToken);
        logger.LogInformation("Clipboard monitoring state: {IsCapturing}", clipboardMonitor.IsCapturing);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return clipboardMonitor.StopAsync(cancellationToken);
    }
}
