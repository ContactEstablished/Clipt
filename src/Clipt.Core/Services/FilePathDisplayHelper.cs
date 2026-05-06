namespace Clipt.Core.Services;

public static class FilePathDisplayHelper
{
    public static string GetFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.TrimEnd('\\', '/');
        if (trimmed.Length == 0 || IsDriveRoot(trimmed))
        {
            return string.Empty;
        }

        var lastSep = trimmed.LastIndexOfAny(['\\', '/']);
        if (lastSep < 0)
        {
            return trimmed;
        }

        var name = trimmed[(lastSep + 1)..];
        return string.IsNullOrEmpty(name) ? string.Empty : name;
    }

    private static bool IsDriveRoot(string path)
    {
        return path.Length == 2 && path[1] == ':' && char.IsLetter(path[0]);
    }

    public static string GetParentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.TrimEnd('\\', '/');
        var lastSep = trimmed.LastIndexOfAny(['\\', '/']);
        if (lastSep < 0)
        {
            return string.Empty;
        }

        return trimmed[..lastSep];
    }

    public static string GetExtensionLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Folder";
        }

        var trimmed = path.TrimEnd('\\', '/');
        if (trimmed.Length == 0)
        {
            return "Folder";
        }

        var name = GetFileName(trimmed);
        if (string.IsNullOrEmpty(name))
        {
            return "Folder";
        }

        var dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1)
        {
            return "Folder";
        }

        return name[(dot + 1)..].ToUpperInvariant();
    }

    public static string GetKindLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Folder";
        }

        if (Directory.Exists(path))
        {
            return "Folder";
        }

        if (File.Exists(path))
        {
            return "File";
        }

        return HasExtension(path) ? "File" : "Folder";
    }

    public static bool Exists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return File.Exists(path) || Directory.Exists(path);
    }

    public static string FormatCountSummary(IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0)
        {
            return string.Empty;
        }

        return paths.Count == 1 ? "1 item" : $"{paths.Count} items";
    }

    private static bool HasExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.TrimEnd('\\', '/');
        var name = GetFileName(trimmed);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1;
    }
}
