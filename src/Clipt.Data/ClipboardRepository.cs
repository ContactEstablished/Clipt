using Clipt.Core.Models;
using Clipt.Core.Services;

namespace Clipt.Data;

public sealed class ClipboardRepository : IHistoryService
{
    public Task<IReadOnlyList<ClipboardItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
    }
}
