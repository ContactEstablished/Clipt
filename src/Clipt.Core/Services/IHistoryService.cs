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
}
