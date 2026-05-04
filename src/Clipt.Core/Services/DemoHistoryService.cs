using Clipt.Core.Models;

namespace Clipt.Core.Services;

public sealed class DemoHistoryService : IHistoryService
{
    public Task<IReadOnlyList<ClipboardItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DesignTimeData.GetSampleItems());
    }
}
