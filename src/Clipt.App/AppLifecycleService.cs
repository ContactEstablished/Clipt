using Clipt.App.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clipt.App;

public sealed class AppLifecycleService : IHostedService
{
    private readonly IHotkeyService _hotkeyService;
    private readonly ILogger<AppLifecycleService> _logger;

    public AppLifecycleService(IHotkeyService hotkeyService, ILogger<AppLifecycleService> logger)
    {
        _hotkeyService = hotkeyService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clipt starting.");

        await _hotkeyService.RegisterFromSettingsAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clipt stopping.");
        _hotkeyService.Unregister();
        return Task.CompletedTask;
    }
}
