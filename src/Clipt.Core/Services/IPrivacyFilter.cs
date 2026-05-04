using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IPrivacyFilter
{
    bool ShouldCapture(ClipboardItem item);
}
