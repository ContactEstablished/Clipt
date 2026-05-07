namespace Clipt.Core.Services;

/// <summary>
/// Normalizes ignored-entry lists from the Settings &gt; Privacy textboxes
/// before they are persisted to <see cref="Models.AppSettings"/>.
///
/// Behavior:
/// <list type="bullet">
///   <item>Each entry is trimmed of leading and trailing whitespace.</item>
///   <item>Entries that become empty after trimming are dropped.</item>
///   <item>Duplicates are removed case-insensitively, preserving the order
///         and casing of the first occurrence.</item>
/// </list>
///
/// The matcher (<see cref="PrivacyFilter"/>) compares with
/// <see cref="System.StringComparison.OrdinalIgnoreCase"/>, so retaining the
/// user's original casing is purely cosmetic — it lets the textbox round-trip
/// preserve what the user typed.
/// </summary>
public static class PrivacyEntryNormalizer
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? entries)
    {
        if (entries is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var raw in entries)
        {
            if (raw is null)
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
