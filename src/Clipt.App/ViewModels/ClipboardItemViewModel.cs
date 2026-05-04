using Clipt.Core.Models;

namespace Clipt.App.ViewModels;

public sealed class ClipboardItemViewModel(ClipboardItem model)
{
    public ClipboardItem Model { get; } = model;

    public Guid Id => Model.Id;

    public string Title => Model.Title;

    public string PreviewText => Model.PreviewText;

    public string Content => Model.Content;

    public ContentType ContentType => Model.ContentType;

    public string SourceAppName => Model.SourceAppName;

    public DateTimeOffset CreatedAt => Model.CreatedAt;

    public bool IsPinned => Model.IsPinned;

    public string? Language => Model.Language;

    public string? ImageUri => Model.ImageUri;

    public IReadOnlyList<string> FilePaths => Model.FilePaths;

    public string GroupName => IsPinned ? "Pinned" : "Recent";
}
