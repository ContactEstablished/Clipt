using System.Text.RegularExpressions;
using Clipt.Core.Models;

namespace Clipt.Core.Services;

/// <summary>
/// Filters clipboard items based on privacy settings.
/// When no settings are provided, only empty/whitespace content is rejected.
///
/// Pattern matching rules:
/// - If a pattern starts with <c>regex:</c> (case-insensitive), the remainder
///   is treated as a .NET regular expression and matched case-insensitively
///   against title, preview, and content. Invalid regex patterns are silently
///   ignored (logged in production via caller).
/// - Otherwise, the pattern is treated as a case-insensitive substring
///   matched against title, preview, and content.
/// </summary>
public sealed class PrivacyFilter : IPrivacyFilter
{
    public bool ShouldCapture(ClipboardItem item, AppSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(item.Content))
        {
            return false;
        }

        if (settings is null)
        {
            return true;
        }

        if (IsIgnoredByAppName(item, settings))
        {
            return false;
        }

        if (IsIgnoredByAppPath(item, settings))
        {
            return false;
        }

        if (IsIgnoredByPattern(item, settings))
        {
            return false;
        }

        return true;
    }

    private static bool IsIgnoredByAppName(ClipboardItem item, AppSettings settings)
    {
        if (settings.IgnoredAppNames.Count == 0)
        {
            return false;
        }

        return settings.IgnoredAppNames.Any(name =>
            string.Equals(item.SourceAppName, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsIgnoredByAppPath(ClipboardItem item, AppSettings settings)
    {
        if (settings.IgnoredAppPaths.Count == 0)
        {
            return false;
        }

        var sourcePath = item.SourceAppPath;
        if (string.IsNullOrEmpty(sourcePath))
        {
            return false;
        }

        return settings.IgnoredAppPaths.Any(ignored =>
            sourcePath.Equals(ignored, StringComparison.OrdinalIgnoreCase) ||
            sourcePath.StartsWith(ignored, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsIgnoredByPattern(ClipboardItem item, AppSettings settings)
    {
        if (settings.IgnoredPatterns.Count == 0)
        {
            return false;
        }

        foreach (var pattern in settings.IgnoredPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                var regexPattern = pattern[6..];
                if (string.IsNullOrEmpty(regexPattern))
                {
                    continue;
                }

                try
                {
                    if (IsMatch(item, regexPattern))
                    {
                        return true;
                    }
                }
                catch (RegexParseException)
                {
                    // Invalid regex — skip this pattern safely.
                }
            }
            else
            {
                if (IsSubstringMatch(item, pattern))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsMatch(ClipboardItem item, string regexPattern)
    {
        return Regex.IsMatch(item.Title ?? string.Empty, regexPattern, RegexOptions.IgnoreCase)
            || Regex.IsMatch(item.PreviewText ?? string.Empty, regexPattern, RegexOptions.IgnoreCase)
            || Regex.IsMatch(item.Content ?? string.Empty, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsSubstringMatch(ClipboardItem item, string pattern)
    {
        return (item.Title ?? string.Empty).Contains(pattern, StringComparison.OrdinalIgnoreCase)
            || (item.PreviewText ?? string.Empty).Contains(pattern, StringComparison.OrdinalIgnoreCase)
            || (item.Content ?? string.Empty).Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
