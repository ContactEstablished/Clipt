using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IClipboardWriter
{
    Task WriteAsync(ClipboardItem item, ClipboardWriteOptions options, CancellationToken cancellationToken);
}
