using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clipt.App;

public sealed class AppLifecycleService(ILogger<AppLifecycleService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Clipt Phase 1 shell starting.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Clipt Phase 1 shell stopping.");
        return Task.CompletedTask;
    }
}
