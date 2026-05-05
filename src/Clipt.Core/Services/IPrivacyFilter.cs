using Clipt.Core.Models;

namespace Clipt.Core.Services;

/// <summary>
/// Determines whether clipboard content should be captured, based on
/// content validity and optional privacy settings.
/// </summary>
public interface IPrivacyFilter
{
    /// <summary>
    /// Returns <c>true</c> when the clipboard item should be captured and stored.
    /// When <paramref name="settings"/> is <c>null</c>, only basic content
    /// validity checks are performed (empty/whitespace content is rejected).
    /// </summary>
    /// <param name="item">The candidate clipboard item.</param>
    /// <param name="settings">
    /// Optional privacy settings. When provided, ignored apps, paths, and
    /// content patterns are also checked.
    /// </param>
    bool ShouldCapture(ClipboardItem item, AppSettings? settings = null);
}
