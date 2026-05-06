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

    /// <summary>
    /// Deletes the cached preview file referenced by <paramref name="imageUri"/>.
    /// Only files that live inside the preview cache directory are deleted; any URI
    /// pointing elsewhere is silently skipped. Null, empty, or malformed URIs are ignored.
    /// </summary>
    public void TryDeletePreview(string? imageUri)
    {
        if (string.IsNullOrWhiteSpace(imageUri))
        {
            return;
        }

        try
        {
            var localPath = UriToLocalPath(imageUri);
            if (localPath is null)
            {
                return;
            }

            if (!IsInsideCacheDirectory(localPath))
            {
                _logger.LogWarning(
                    "Refusing to delete preview file outside cache directory: '{Path}'.", localPath);
                return;
            }

            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                _logger.LogDebug("Deleted preview cache file '{Path}'.", localPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete preview cache file for URI '{Uri}'.", imageUri);
        }
    }

    /// <summary>
    /// Deletes cached preview files for a batch of image URIs.
    /// Each URI is validated and silently skipped when null/empty/outside the cache directory.
    /// </summary>
    public void TryDeletePreviews(IEnumerable<string?> imageUris)
    {
        foreach (var uri in imageUris)
        {
            TryDeletePreview(uri);
        }
    }

    private string? UriToLocalPath(string imageUri)
    {
        if (Uri.TryCreate(imageUri, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        // Accept bare rooted local paths as a fallback.
        if (Path.IsPathRooted(imageUri))
        {
            return imageUri;
        }

        return null;
    }

    private bool IsInsideCacheDirectory(string localPath)
    {
        var fullPath = Path.GetFullPath(localPath);
        var cacheDir = Path.GetFullPath(_cacheDirectory);

        // Require the file to be a direct child of (or nested inside) the cache directory.
        return fullPath.StartsWith(cacheDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(cacheDir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
