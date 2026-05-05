namespace Clipt.Core.Models;

public sealed record ClipboardItem
{
    public required Guid Id { get; init; }

    public required string ContentHash { get; init; }

    public required string Title { get; init; }

    public required string PreviewText { get; init; }

    public required string Content { get; init; }

    public required ContentType ContentType { get; init; }

    public required string SourceAppName { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public bool IsPinned { get; init; }

    public int? PinOrder { get; init; }

    public bool IsFavorite { get; init; }

    public string? Language { get; init; }

    public string? ImageUri { get; init; }

    public string? SourceAppPath { get; init; }

    public IReadOnlyList<string> FilePaths { get; init; } = [];

    public IReadOnlyList<ClipboardFormat> Formats { get; init; } = [];

    public long ByteSize { get; init; }

    public DateTimeOffset LastUsedAt { get; init; }

    public int UseCount { get; init; }
}
