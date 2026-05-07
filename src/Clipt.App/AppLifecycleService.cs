using Clipt.App.Services;
using Clipt.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clipt.App;

public sealed class AppLifecycleService : IHostedService
{
    private readonly IHotkeyService _hotkeyService;
    private readonly IHistoryService _historyService;
    private readonly ImagePreviewCache _imagePreviewCache;
    private readonly ILogger<AppLifecycleService> _logger;

    public AppLifecycleService(
        IHotkeyService hotkeyService,
        IHistoryService historyService,
        ImagePreviewCache imagePreviewCache,
        ILogger<AppLifecycleService> logger)
    {
        _hotkeyService = hotkeyService;
        _historyService = historyService;
        _imagePreviewCache = imagePreviewCache;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clipt starting.");

        await ReconcileImagePreviewCacheAsync(cancellationToken);
        await _hotkeyService.RegisterFromSettingsAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clipt stopping.");
        _hotkeyService.Unregister();
        return Task.CompletedTask;
    }

    private async Task ReconcileImagePreviewCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var imageUris = await _historyService.GetImageUrisAsync(cancellationToken);
            var referencedUris = new HashSet<string>(imageUris);
            var result = _imagePreviewCache.ReconcileCache(referencedUris);
            _logger.LogInformation(
                "Startup preview cache reconciliation: {Scanned} scanned, {Deleted} deleted, {Skipped} skipped.",
                result.Scanned, result.Deleted, result.Skipped);
        }
        catch (OperationCanceledException)
        {
            // Swallow cancellation.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preview cache reconciliation failed during startup.");
        }
    }
}
