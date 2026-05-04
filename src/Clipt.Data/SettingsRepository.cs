using Clipt.Core.Models;

namespace Clipt.Data;

public sealed class SettingsRepository
{
    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AppSettings());
    }
}
