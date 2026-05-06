using Clipt.Core.Services;

namespace Clipt.App.ViewModels;

public sealed class FilePathPreviewViewModel
{
    public FilePathPreviewViewModel(string fullPath)
    {
        FullPath = fullPath;
        Name = FilePathDisplayHelper.GetFileName(fullPath);
        ParentPath = FilePathDisplayHelper.GetParentPath(fullPath);
        ExtensionLabel = FilePathDisplayHelper.GetExtensionLabel(fullPath);
        KindLabel = FilePathDisplayHelper.GetKindLabel(fullPath);
        Exists = FilePathDisplayHelper.Exists(fullPath);
        StatusLabel = Exists ? "Available" : "Missing";
    }

    public string FullPath { get; }
    public string Name { get; }
    public string ParentPath { get; }
    public string ExtensionLabel { get; }
    public string KindLabel { get; }
    public string KindGlyph => KindLabel == "File" ? "Fi" : "Fo";
    public bool Exists { get; }
    public string StatusLabel { get; }
    public bool IsMissing => !Exists;
}
