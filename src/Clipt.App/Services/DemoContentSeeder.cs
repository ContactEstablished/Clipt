using Clipt.Core;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

public sealed class DemoContentSeeder
{
    private readonly IHistoryService _historyService;
    private readonly ILogger<DemoContentSeeder> _logger;

    public DemoContentSeeder(IHistoryService historyService, ILogger<DemoContentSeeder> logger)
    {
        _historyService = historyService;
        _logger = logger;
    }

    public async Task<SeedResult> SeedAsync(CancellationToken cancellationToken)
    {
        var items = DesignTimeData.GetSampleItems();
        var inserted = 0;
        var updated = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The item is not yet in the database so SaveAsync will insert it.
            // If the content hash already exists (re-click), SaveAsync updates
            // last_used_at and use_count and returns the existing row.
            var saved = await _historyService.SaveAsync(item, cancellationToken);

            if (saved.Id == item.Id)
            {
                inserted++;
                _logger.LogDebug("Inserted demo item {Title} ({ContentType}).", item.Title, item.ContentType);
            }
            else
            {
                updated++;
                _logger.LogDebug("Skipped demo item {Title} — content hash already exists.", item.Title);
            }
        }

        _logger.LogInformation(
            "Demo content seed complete: {Inserted} inserted, {Updated} skipped (already present).",
            inserted,
            updated);

        return new SeedResult(inserted, updated);
    }
}

public readonly record struct SeedResult(int Inserted, int Updated)
{
    public int Total => Inserted + Updated;
}
