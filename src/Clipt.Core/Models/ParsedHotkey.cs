namespace Clipt.Core.Models;

/// <summary>
/// Result of parsing a hotkey gesture string. Key and Modifiers use the
/// same casing as <see cref="System.Windows.Input.Key"/> and the canonical
/// modifier names ("Ctrl", "Alt", "Shift", "Win").
/// </summary>
public sealed record ParsedHotkey(string Key, IReadOnlyList<string> Modifiers);
