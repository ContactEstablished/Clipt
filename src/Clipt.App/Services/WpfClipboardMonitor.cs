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
/// Monitors the system clipboard for text changes and emits
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
    private readonly ILogger<WpfClipboardMonitor> _logger;

    private HwndSource? _messageSource;
    private string? _lastTextHash;
    private AppSettings? _cachedSettings;
    private bool _isDisposed;

    public WpfClipboardMonitor(
        IContentTypeDetector contentTypeDetector,
        IPrivacyFilter privacyFilter,
        ISourceAppResolver sourceAppResolver,
        ISettingsService settingsService,
        ILogger<WpfClipboardMonitor> logger)
    {
        _contentTypeDetector = contentTypeDetector;
        _privacyFilter = privacyFilter;
        _sourceAppResolver = sourceAppResolver;
        _settingsService = settingsService;
        _logger = logger;
    }

    public event EventHandler<ClipboardItem>? ClipboardItemCaptured;

    public bool IsCapturing { get; private set; }

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

        handled = true;
        CaptureCurrentTextClipboardItem();
        return 0;
    }

    private void CaptureCurrentTextClipboardItem()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            var text = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var hash = ClipboardContentHasher.ComputeHash(text);
            if (hash == _lastTextHash)
            {
                return;
            }

            var item = CreateTextItem(text);
            if (!_privacyFilter.ShouldCapture(item, _cachedSettings))
            {
                _logger.LogDebug(
                    "Clipboard item from '{SourceApp}' blocked by privacy filter.",
                    item.SourceAppName);
                return;
            }

            _lastTextHash = hash;
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

    private ClipboardItem CreateTextItem(string text)
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
            Formats =
            [
                new ClipboardFormat("CF_UNICODETEXT", text),
            ],
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
