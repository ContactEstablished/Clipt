using Clipt.Core.Models;

namespace Clipt.Core.Services;

public sealed class PrivacyFilter : IPrivacyFilter
{
    public bool ShouldCapture(ClipboardItem item) => !string.IsNullOrWhiteSpace(item.Content);
}
