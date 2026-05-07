using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Clipt.Interop;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

/// <summary>
/// Monitors the system clipboard for supported content changes and emits
/// <see cref="ClipboardItemCaptured"/> events. Uses Win32 clipboard
/// format listener messages for reliable detection.
///
/// Source app metadata is resolved via <see cref="ISourceAppResolver"/>.
/// Privacy settings are cached at startup from <see cref="ISettingsService"/>
/// and passed to the <see cref="IPrivacyFilter"/> on each capture.
/// </summary>
public sealed class WpfClipboardMonitor : IClipboardMonitor, IDisposable
{
    private readonly IContentTypeDetector _contentTypeDetector;
    private readonly IPrivacyFilter _privacyFilter;
    private readonly ISourceAppResolver _sourceAppResolver;
    private readonly ISettingsService _settingsService;
    private readonly ImagePreviewCache _imagePreviewCache;
    private readonly ILogger<WpfClipboardMonitor> _logger;

    private HwndSource? _messageSource;
    private string? _lastContentHash;

    // History deletion invalidates in-memory duplicate tracking because
    // a previously-captured hash may no longer exist in the database.
    // Callers must invoke ResetDuplicateTracking after removing items.

    private AppSettings? _cachedSettings;
    private bool _isPaused;
    private bool _isDisposed;

    public WpfClipboardMonitor(
        IContentTypeDetector contentTypeDetector,
        IPrivacyFilter privacyFilter,
        ISourceAppResolver sourceAppResolver,
        ISettingsService settingsService,
        ImagePreviewCache imagePreviewCache,
        ILogger<WpfClipboardMonitor> logger)
    {
        _contentTypeDetector = contentTypeDetector;
        _privacyFilter = privacyFilter;
        _sourceAppResolver = sourceAppResolver;
        _settingsService = settingsService;
        _imagePreviewCache = imagePreviewCache;
        _logger = logger;
    }

    public event EventHandler<ClipboardItem>? ClipboardItemCaptured;

    public bool IsCapturing { get; private set; }

    public bool IsPaused => _isPaused;

    public event EventHandler? CaptureStateChanged;

    public Task SetPausedAsync(bool paused, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_isPaused == paused)
        {
            return Task.CompletedTask;
        }

        _isPaused = paused;
        _logger.LogInformation("Clipboard capture {State}.", paused ? "paused" : "resumed");

        // Keep the cached settings consistent so that a subsequent
        // settings save includes the correct capture pause flag.
        if (_cachedSettings is not null)
        {
            _cachedSettings = _cachedSettings with { IsCapturePaused = paused };
        }

        CaptureStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void ResetDuplicateTracking()
    {
        _lastContentHash = null;
        _logger.LogDebug("Duplicate tracking reset after history mutation.");
    }

    private bool IsWithinSizeLimit(long byteSize)
    {
        var limit = _cachedSettings?.MaxClipboardItemBytes ?? 0;
        return ClipboardCaptureSizeGuard.IsWithinLimit(byteSize, limit);
    }

    public async Task RefreshCachedPrivacySettingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fresh = await _settingsService.GetAsync(cancellationToken);
            _cachedSettings = fresh.Normalize() with { IsCapturePaused = _isPaused };
            _logger.LogDebug("Clipboard monitor privacy cache refreshed from settings.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to refresh privacy cache; keeping previous cached settings.");
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsCapturing)
        {
            return;
        }

        // Cache privacy settings at startup. If loading fails, defaults are used
        // so monitoring is never blocked by a settings read error.
        try
        {
            _cachedSettings = await _settingsService.GetAsync(cancellationToken);
            _logger.LogDebug("Privacy settings cached for clipboard monitor.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to load privacy settings; using permissive defaults.");
            _cachedSettings = new AppSettings();
        }

        // Apply persisted capture pause state before any clipboard events
        // are processed. This runs inside StartAsync so the listener is not
        // registered yet, guaranteeing no capture races.
        if (_cachedSettings.IsCapturePaused)
        {
            _isPaused = true;
            _logger.LogInformation("Clipboard capture starts in paused mode (persisted setting).");
        }

        var parameters = new HwndSourceParameters("CliptClipboardListener")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };

        _messageSource = new HwndSource(parameters);
        _messageSource.AddHook(WndProc);

        if (!NativeMethods.AddClipboardFormatListener(_messageSource.Handle))
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogWarning("Unable to register clipboard listener. Win32 error: {Error}", error);
            _messageSource.RemoveHook(WndProc);
            _messageSource.Dispose();
            _messageSource = null;
            return;
        }

        IsCapturing = true;
        _logger.LogInformation("Clipboard listener registered.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopListener();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        StopListener();
        _isDisposed = true;
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != NativeMethods.WmClipboardUpdate)
        {
            return 0;
        }

        _logger.LogDebug("Clipboard update message received.");
        handled = true;
        CaptureCurrentClipboardItem();
        return 0;
    }

    private void CaptureCurrentClipboardItem()
    {
        if (_isPaused)
        {
            _logger.LogDebug("Clipboard update ignored: capture is paused.");
            return;
        }

        try
        {
            if (TryCaptureFileDropClipboardItem())
            {
                return;
            }

            if (TryCaptureImageClipboardItem())
            {
                return;
            }

            if (!Clipboard.ContainsText())
            {
                _logger.LogDebug("Clipboard update ignored: no supported clipboard content.");
                return;
            }

            var text = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogDebug("Clipboard update ignored: text is null or whitespace.");
                return;
            }

            var hash = ClipboardContentHasher.ComputeHash(text);
            if (hash == _lastContentHash)
            {
                _logger.LogDebug("Clipboard update ignored: duplicate hash {Hash}.", hash);
                return;
            }

            // Capture all text-related formats (Unicode + ANSI + HTML + RTF)
            // before applying the size guard so the cap covers every payload
            // we would persist — not just the Unicode variant. Browser/Word
            // copies routinely have small Unicode text but very large HTML/RTF.
            var formats = CaptureTextFormats(text);
            var totalBytes = ClipboardFormatHelper.SumPayloadBytes(formats);
            if (!IsWithinSizeLimit(totalBytes))
            {
                _logger.LogDebug(
                    "Clipboard text dropped: total payload {Bytes}B across {Count} format(s) exceeds configured limit.",
                    totalBytes, formats.Count);
                return;
            }

            var item = CreateTextItem(text, formats);
            if (!_privacyFilter.ShouldCapture(item, _cachedSettings))
            {
                _logger.LogDebug(
                    "Clipboard item from '{SourceApp}' blocked by privacy filter.",
                    item.SourceAppName);
                return;
            }

            _lastContentHash = hash;
            _logger.LogDebug("Clipboard item captured: '{Title}' (hash {Hash}).", item.Title, hash);
            ClipboardItemCaptured?.Invoke(this, item);
        }
        catch (ExternalException exception)
        {
            _logger.LogDebug(exception, "Clipboard was temporarily unavailable.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogDebug(exception, "Clipboard text capture failed.");
        }
    }

    private bool TryCaptureFileDropClipboardItem()
    {
        try
        {
            if (!Clipboard.ContainsFileDropList())
            {
                return false;
            }

            var fileDropList = Clipboard.GetFileDropList();
            var paths = fileDropList
                .Cast<string>()
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count == 0)
            {
                _logger.LogDebug("Clipboard file drop ignored: no usable paths.");
                return true;
            }

            var content = string.Join(Environment.NewLine, paths);
            var hash = ClipboardContentHasher.ComputeHash(content);
            if (hash == _lastContentHash)
            {
                _logger.LogDebug("Clipboard file drop ignored: duplicate hash {Hash}.", hash);
                return true;
            }

            var byteSize = Encoding.UTF8.GetByteCount(content);
            if (!IsWithinSizeLimit(byteSize))
            {
                _logger.LogDebug(
                    "Clipboard file drop dropped: size {Bytes}B exceeds configured limit.", byteSize);
                return true;
            }

            var formats = new List<ClipboardFormat>
            {
                new(ClipboardFormatNames.FileDrop, content),
                new(ClipboardFormatNames.UnicodeText, content),
            };

            var item = CreateFileDropItem(paths, content, formats);
            if (!_privacyFilter.ShouldCapture(item, _cachedSettings))
            {
                _logger.LogDebug(
                    "Clipboard file drop from '{SourceApp}' blocked by privacy filter.",
                    item.SourceAppName);
                return true;
            }

            _lastContentHash = hash;
            _logger.LogDebug(
                "Clipboard file drop captured: '{Title}' ({PathCount} paths, hash {Hash}).",
                item.Title,
                paths.Count,
                hash);
            ClipboardItemCaptured?.Invoke(this, item);
            return true;
        }
        catch (ExternalException exception)
        {
            _logger.LogDebug(exception, "Clipboard was temporarily unavailable during file drop capture.");
            return true;
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogDebug(exception, "Clipboard file drop capture failed.");
            return true;
        }
    }

    private bool TryCaptureImageClipboardItem()
    {
        try
        {
            if (!Clipboard.ContainsImage())
            {
                return false;
            }

            var bitmap = Clipboard.GetImage();
            if (bitmap is null)
            {
                _logger.LogDebug("Clipboard image was null; skipping.");
                return false;
            }

            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var estimatedSize = ImageClipboardMetadataHelper.EstimateRgbaByteSize(width, height);

            // Reject oversize images before writing to the preview cache so we
            // don't orphan a cache file on disk.
            if (!IsWithinSizeLimit(estimatedSize))
            {
                _logger.LogDebug(
                    "Clipboard image dropped: estimated size {Bytes}B exceeds configured limit ({Width}x{Height}).",
                    estimatedSize, width, height);
                return true;
            }

            var title = ImageClipboardMetadataHelper.CreateTitle(width, height);
            var previewText = ImageClipboardMetadataHelper.CreatePreviewText(width, height, estimatedSize);

            var imageUri = _imagePreviewCache.SavePreview(bitmap);

            // Include the preview filename (or timestamp) in the hash so that
            // two distinct screenshots with the same dimensions produce different hashes.
            var hashInput = $"image:{width}x{height}:{imageUri ?? DateTimeOffset.UtcNow.Ticks.ToString()}";
            var hash = ClipboardContentHasher.ComputeHash(hashInput);

            if (hash == _lastContentHash)
            {
                _logger.LogDebug("Clipboard image ignored: duplicate hash {Hash}.", hash);
                return true;
            }

            var metaText = $"width={width};height={height};bytes={estimatedSize}";
            var formats = new List<ClipboardFormat>
            {
                new(ClipboardFormatNames.Bitmap, metaText),
            };

            var source = _sourceAppResolver.Resolve();
            var now = DateTimeOffset.Now;

            var item = new ClipboardItem
            {
                Id = Guid.NewGuid(),
                ContentHash = hash,
                Title = title,
                PreviewText = previewText,
                Content = metaText,
                ContentType = ContentType.Image,
                SourceAppName = source.Name,
                SourceAppPath = source.Path,
                CreatedAt = now,
                ByteSize = estimatedSize,
                LastUsedAt = now,
                UseCount = 0,
                ImageUri = imageUri,
                Formats = formats,
            };

            if (!_privacyFilter.ShouldCapture(item, _cachedSettings))
            {
                _logger.LogDebug(
                    "Clipboard image from '{SourceApp}' blocked by privacy filter.",
                    item.SourceAppName);
                return true;
            }

            _lastContentHash = hash;
            _logger.LogDebug(
                "Clipboard image captured: '{Title}' {Width}x{Height} (hash {Hash}).",
                title, width, height, hash);
            ClipboardItemCaptured?.Invoke(this, item);
            return true;
        }
        catch (ExternalException exception)
        {
            _logger.LogDebug(exception, "Clipboard was temporarily unavailable during image capture.");
            return true;
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogDebug(exception, "Clipboard image capture failed.");
            return true;
        }
    }

    private ClipboardItem CreateTextItem(string text, IReadOnlyList<ClipboardFormat> formats)
    {
        var contentType = _contentTypeDetector.Detect(text);
        var title = CreateTitle(text, contentType);
        var preview = text.ReplaceLineEndings(" ");
        if (preview.Length > 180)
        {
            preview = string.Concat(preview.AsSpan(0, 177), "...");
        }

        // Resolve the source application at capture time.
        // The ForegroundWindowTracker may have captured the previous window
        // before Clipt took focus; the resolver handles that internally.
        var source = _sourceAppResolver.Resolve();

        var now = DateTimeOffset.Now;

        return new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = ClipboardContentHasher.ComputeHash(text),
            Title = title,
            PreviewText = preview,
            Content = text,
            ContentType = contentType,
            SourceAppName = source.Name,
            SourceAppPath = source.Path,
            CreatedAt = now,
            ByteSize = Encoding.UTF8.GetByteCount(text),
            LastUsedAt = now,
            UseCount = 0,
            Formats = formats,
        };
    }

    private ClipboardItem CreateFileDropItem(
        IReadOnlyList<string> paths,
        string content,
        IReadOnlyList<ClipboardFormat> formats)
    {
        var source = _sourceAppResolver.Resolve();
        var now = DateTimeOffset.Now;
        var title = CreateFileDropTitle(paths);
        var preview = paths.Count == 1
            ? paths[0]
            : $"{paths.Count} files and folders";

        return new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = ClipboardContentHasher.ComputeHash(content),
            Title = title,
            PreviewText = preview,
            Content = content,
            ContentType = ContentType.File,
            SourceAppName = source.Name,
            SourceAppPath = source.Path,
            CreatedAt = now,
            ByteSize = Encoding.UTF8.GetByteCount(content),
            LastUsedAt = now,
            UseCount = 0,
            FilePaths = paths,
            Formats = formats,
        };
    }

    private static string CreateTitle(string text, ContentType contentType)
    {
        var firstLine = text
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return contentType.ToString();
        }

        return firstLine.Length <= 64 ? firstLine : string.Concat(firstLine.AsSpan(0, 61), "...");
    }

    private static string CreateFileDropTitle(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1)
        {
            var name = Path.GetFileName(paths[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(name) ? paths[0] : name;
        }

        return $"{paths.Count} files";
    }

    /// <summary>
    /// Captures available text-based clipboard formats at capture time.
    /// Each format is probed independently and failures are silently ignored
    /// so one unavailable format does not block capture of the others.
    /// </summary>
    private static List<ClipboardFormat> CaptureTextFormats(string unicodeText)
    {
        var formats = new List<ClipboardFormat>
        {
            new(DataFormats.UnicodeText, unicodeText),
        };

        // ANSI text
        try
        {
            if (Clipboard.ContainsText(TextDataFormat.Text))
            {
                var text = Clipboard.GetText(TextDataFormat.Text);
                if (!string.IsNullOrEmpty(text))
                {
                    formats.Add(new ClipboardFormat(DataFormats.Text, text));
                }
            }
        }
        catch (ExternalException) { /* clipboard unavailable during format probe */ }
        catch (InvalidOperationException) { /* transient failure, skip this format */ }

        // HTML Format
        try
        {
            if (Clipboard.ContainsData(DataFormats.Html))
            {
                var html = Clipboard.GetData(DataFormats.Html) as string;
                if (!string.IsNullOrEmpty(html))
                {
                    formats.Add(new ClipboardFormat(DataFormats.Html, html));
                }
            }
        }
        catch (ExternalException) { }
        catch (InvalidOperationException) { }

        // Rich Text Format
        try
        {
            if (Clipboard.ContainsData(DataFormats.Rtf))
            {
                var rtf = Clipboard.GetData(DataFormats.Rtf) as string;
                if (!string.IsNullOrEmpty(rtf))
                {
                    formats.Add(new ClipboardFormat(DataFormats.Rtf, rtf));
                }
            }
        }
        catch (ExternalException) { }
        catch (InvalidOperationException) { }

        return formats;
    }

    private void StopListener()
    {
        if (_messageSource is null)
        {
            IsCapturing = false;
            return;
        }

        if (IsCapturing && !NativeMethods.RemoveClipboardFormatListener(_messageSource.Handle))
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogDebug("Unable to unregister clipboard listener. Win32 error: {Error}", error);
        }

        _messageSource.RemoveHook(WndProc);
        _messageSource.Dispose();
        _messageSource = null;
        IsCapturing = false;
    }
}
