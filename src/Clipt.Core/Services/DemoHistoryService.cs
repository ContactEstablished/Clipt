using Clipt.Core.Models;

namespace Clipt.Core.Services;

public sealed class DemoHistoryService : IHistoryService
{
    public Task<IReadOnlyList<ClipboardItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DesignTimeData.GetSampleItems());
    }

    public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = DesignTimeData.GetSampleItems();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(items);
        }

        var results = items
            .Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.PreviewText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.ContentType.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<ClipboardItem>>(results);
    }

    public Task<ClipboardItem> SaveAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(item);
    }

    public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
