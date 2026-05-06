namespace Clipt.Core.Services;

public static class ImageClipboardMetadataHelper
{
    public static string CreateTitle(int width, int height)
        => $"Image {FormatPixelSize(width, height)}";

    public static string CreatePreviewText(int width, int height, long byteSize)
        => $"{FormatPixelSize(width, height)} - {FormatByteSize(byteSize)}";

    public static long EstimateRgbaByteSize(int width, int height)
        => (long)width * height * 4;

    public static string FormatPixelSize(int width, int height)
        => $"{width} x {height}";

    private static string FormatByteSize(long bytes)
    {
        if (bytes < 1024L) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
