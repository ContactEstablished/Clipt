using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IHistoryService
{
    Task<IReadOnlyList<ClipboardItem>> GetItemsAsync(CancellationToken cancellationToken);
}
