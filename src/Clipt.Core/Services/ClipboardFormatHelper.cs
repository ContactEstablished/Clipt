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

    /// <summary>
    /// Returns the total stored bytes for a single format, counting BOTH the
    /// UTF-8 byte count of <see cref="ClipboardFormat.TextPayload"/> and the
    /// length of <see cref="ClipboardFormat.BinaryPayload"/> when present.
    /// Use this when the goal is to measure how much will be persisted (the
    /// size cap on capture), not the display size for UI badges.
    /// </summary>
    public static long GetPayloadByteCount(ClipboardFormat format)
    {
        var bytes = 0L;
        if (format.TextPayload is { Length: > 0 } text)
        {
            bytes += Encoding.UTF8.GetByteCount(text);
        }

        if (format.BinaryPayload is { Length: > 0 } binary)
        {
            bytes += binary.Length;
        }

        return bytes;
    }

    /// <summary>
    /// Sums <see cref="GetPayloadByteCount"/> across every format in the list.
    /// Used by the capture pipeline to enforce the per-clip size limit against
    /// the actual stored payload (Unicode + ANSI + HTML + RTF, etc.) rather
    /// than just the primary text variant.
    /// </summary>
    public static long SumPayloadBytes(IReadOnlyList<ClipboardFormat> formats)
    {
        var total = 0L;
        for (var i = 0; i < formats.Count; i++)
        {
            total += GetPayloadByteCount(formats[i]);
        }

        return total;
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
