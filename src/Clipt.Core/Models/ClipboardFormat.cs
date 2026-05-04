namespace Clipt.Core.Models;

public sealed record ClipboardFormat(
    string Name,
    string? TextPayload,
    byte[]? BinaryPayload = null);
