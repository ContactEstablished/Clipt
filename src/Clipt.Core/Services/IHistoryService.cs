using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IHistoryService
{
    Task<IReadOnlyList<ClipboardItem>> GetItemsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardItem>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<ClipboardItem> SaveAsync(ClipboardItem item, CancellationToken cancellationToken);

    Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<int> ClearUnpinnedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the oldest unpinned items so that at most <paramref name="maxItems"/>
    /// unpinned rows remain. Pinned items are never deleted.
    /// </summary>
    /// <param name="maxItems">Maximum number of unpinned items to keep. Values &lt;= 0 disable pruning.</param>
    /// <returns>The number of deleted rows.</returns>
    Task<int> PruneUnpinnedAsync(int maxItems, CancellationToken cancellationToken);
}
