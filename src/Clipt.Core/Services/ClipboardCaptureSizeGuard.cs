namespace Clipt.Core.Services;

/// <summary>
/// Decides whether a captured clipboard item is small enough to retain,
/// based on the user-configurable <c>MaxClipboardItemBytes</c> setting.
/// </summary>
public static class ClipboardCaptureSizeGuard
{
    /// <summary>
    /// Returns <c>true</c> if the item size is within the configured limit
    /// (or if the limit is disabled by being &lt;= 0).
    /// </summary>
    /// <param name="byteSize">Size of the candidate clipboard item in bytes.</param>
    /// <param name="maxBytes">Configured maximum. Values &lt;= 0 disable the check.</param>
    public static bool IsWithinLimit(long byteSize, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return true;
        }

        return byteSize <= maxBytes;
    }
}
