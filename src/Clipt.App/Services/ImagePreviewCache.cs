using System.IO;
using System.Windows.Media.Imaging;
using Clipt.Data;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

/// <summary>
/// Manages a local PNG preview cache for image clipboard items.
/// Previews are written to a folder adjacent to the Clipt database directory
/// and referenced via file URIs stored in ClipboardItem.ImageUri.
/// </summary>
public sealed class ImagePreviewCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<ImagePreviewCache> _logger;

    public ImagePreviewCache(DatabasePathProvider pathProvider, ILogger<ImagePreviewCache> logger)
    {
        _logger = logger;

        var dbDir = Path.GetDirectoryName(pathProvider.GetDatabasePath())
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(dbDir, "preview-cache");

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create image preview cache directory at '{Directory}'.", _cacheDirectory);
        }
    }

    /// <summary>
    /// Encodes a BitmapSource as PNG and saves it to the preview cache.
    /// Returns a file URI string suitable for use in ImagePreviewControl, or null on failure.
    /// </summary>
    public string? SavePreview(BitmapSource bitmap)
    {
        try
        {
            var fileName = $"img_{Guid.NewGuid():N}.png";
            var filePath = Path.Combine(_cacheDirectory, fileName);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(filePath);
            encoder.Save(stream);

            return new Uri(filePath).AbsoluteUri;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save image preview to cache.");
            return null;
        }
    }
}
