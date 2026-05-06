using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IHistoryService
{
    Task<IReadOnlyList<ClipboardItem>> GetItemsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardItem>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<ClipboardItem> SaveAsync(ClipboardItem item, CancellationToken cancellationToken);

    Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes all unpinned items and returns the count and image preview URIs that were removed.
    /// Callers are responsible for deleting any referenced preview cache files.
    /// </summary>
    Task<HistoryDeletionResult> ClearUnpinnedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the oldest unpinned items so that at most <paramref name="maxItems"/>
    /// unpinned rows remain. Pinned items are never deleted.
    /// Returns the deleted row count and image preview URIs that were removed;
    /// callers are responsible for deleting any referenced preview cache files.
    /// </summary>
    /// <param name="maxItems">Maximum number of unpinned items to keep. Values &lt;= 0 disable pruning.</param>
    Task<HistoryDeletionResult> PruneUnpinnedAsync(int maxItems, CancellationToken cancellationToken);
}
