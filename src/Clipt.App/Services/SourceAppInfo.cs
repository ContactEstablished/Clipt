namespace Clipt.App.Services;

/// <summary>
/// Metadata about the application that was active when clipboard content was captured.
/// </summary>
public sealed record SourceAppInfo
{
    /// <summary>
    /// Default fallback used when source process info cannot be resolved.
    /// </summary>
    public static readonly SourceAppInfo Unknown = new() { Name = "Unknown", Path = null };

    public required string Name { get; init; }

    public string? Path { get; init; }
}
