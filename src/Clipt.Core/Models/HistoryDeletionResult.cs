namespace Clipt.Core.Models;

/// <summary>
/// Returned by bulk delete operations on the history service.
/// Carries the row count and any image preview URIs that were removed,
/// so callers can clean up the preview cache without the repository touching the filesystem.
/// </summary>
public sealed record HistoryDeletionResult(int Count, IReadOnlyList<string> ImageUris)
{
    public static readonly HistoryDeletionResult Empty = new(0, []);
}
