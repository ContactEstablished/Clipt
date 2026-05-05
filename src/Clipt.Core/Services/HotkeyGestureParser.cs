using Clipt.Core.Models;

namespace Clipt.Core.Services;

/// <summary>
/// Parses hotkey gesture strings such as "Ctrl+Shift+V".
///
/// Accepted format: one or more modifiers separated by '+' followed by exactly
/// one key. Modifier synonyms: Ctrl/Control, Alt, Shift, Win/Windows.
/// Keys must be parseable by <see cref="System.Windows.Input.Key"/>.
/// </summary>
public static class HotkeyGestureParser
{
    private static readonly HashSet<string> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ctrl", "Control", "Alt", "Shift", "Win", "Windows",
    };

    public static ParsedHotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        string? key = null;
        var modifiers = new List<string>();

        foreach (var part in parts)
        {
            if (IsModifier(part))
            {
                modifiers.Add(NormalizeModifier(part));
            }
            else if (key is null)
            {
                key = part;
            }
            else
            {
                // More than one non-modifier key is invalid.
                return null;
            }
        }

        if (key is null || modifiers.Count == 0)
        {
            return null;
        }

        return new ParsedHotkey(key, modifiers);
    }

    private static bool IsModifier(string part)
    {
        return ModifierNames.Contains(part);
    }

    private static string NormalizeModifier(string part)
    {
        return part.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => "Ctrl",
            "ALT" => "Alt",
            "SHIFT" => "Shift",
            "WIN" or "WINDOWS" => "Win",
            _ => part,
        };
    }
}
