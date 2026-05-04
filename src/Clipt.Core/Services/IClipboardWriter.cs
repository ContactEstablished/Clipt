using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IClipboardWriter
{
    Task CopyAsync(ClipboardItem item, CancellationToken cancellationToken);
}
