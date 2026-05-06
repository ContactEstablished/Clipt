using System.Text;
using Clipt.Core.Models;

namespace Clipt.Core.Services;

public static class ClipboardFormatHelper
{
    private static readonly Dictionary<string, string> FriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [ClipboardFormatNames.UnicodeText] = "Unicode text",
        [ClipboardFormatNames.Text] = "ANSI text",
        [ClipboardFormatNames.Html] = "HTML",
        [ClipboardFormatNames.Rtf] = "RTF",
        [ClipboardFormatNames.FileDrop] = "File drop",
    };

    public static string GetFriendlyName(string formatName)
    {
        return FriendlyNames.TryGetValue(formatName, out var friendly)
            ? friendly
            : formatName;
    }

    public static long GetFormatSize(ClipboardFormat format)
    {
        if (format.TextPayload is not null)
        {
            return Encoding.UTF8.GetByteCount(format.TextPayload);
        }

        if (format.BinaryPayload is not null)
        {
            return format.BinaryPayload.Length;
        }

        return 0;
    }

    public static string FormatByteSize(long bytes)
    {
        return bytes switch
        {
            < 0 => "0 B",
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
        };
    }

    public static string GetKindLabel(ClipboardFormat format)
    {
        return format.TextPayload is not null ? "Text" : "Binary";
    }
}
