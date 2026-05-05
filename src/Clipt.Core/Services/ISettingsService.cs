using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface ISettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
