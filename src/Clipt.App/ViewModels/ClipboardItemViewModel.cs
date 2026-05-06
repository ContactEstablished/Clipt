using System.Linq;
using Clipt.Core.Models;
using Clipt.Core.Services;

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

    public IReadOnlyList<ClipboardFormatBadgeViewModel> AvailableFormats { get; } =
        model.Formats.Select(f => new ClipboardFormatBadgeViewModel(f)).ToList();

    public bool HasAvailableFormats => AvailableFormats.Count > 0;

    public IReadOnlyList<FilePathPreviewViewModel> FilePathPreviews { get; } =
        model.FilePaths.Select(p => new FilePathPreviewViewModel(p)).ToList();

    public bool HasFilePathPreviews => FilePathPreviews.Count > 0;

    public ImagePreviewActionsViewModel? ImagePreviewActions { get; } =
        !string.IsNullOrEmpty(model.ImageUri)
            ? new ImagePreviewActionsViewModel(model.ImageUri)
            : null;

    public bool HasImagePreviewActions => ImagePreviewActions is not null;

    public string FilePathSummary => FilePathDisplayHelper.FormatCountSummary(Model.FilePaths);
}
