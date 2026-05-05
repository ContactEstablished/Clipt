using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Clipt.Interop;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

public sealed class WpfClipboardMonitor(
    IContentTypeDetector contentTypeDetector,
    IPrivacyFilter privacyFilter,
    ILogger<WpfClipboardMonitor> logger) : IClipboardMonitor, IDisposable
{
    private HwndSource? _messageSource;
    private string? _lastTextHash;
    private bool _isDisposed;

    public event EventHandler<ClipboardItem>? ClipboardItemCaptured;

    public bool IsCapturing { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsCapturing)
        {
            return Task.CompletedTask;
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
            logger.LogWarning("Unable to register clipboard listener. Win32 error: {Error}", error);
            _messageSource.RemoveHook(WndProc);
            _messageSource.Dispose();
            _messageSource = null;
            return Task.CompletedTask;
        }

        IsCapturing = true;
        logger.LogInformation("Clipboard listener registered.");
        return Task.CompletedTask;
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
            if (!privacyFilter.ShouldCapture(item))
            {
                return;
            }

            _lastTextHash = hash;
            ClipboardItemCaptured?.Invoke(this, item);
        }
        catch (ExternalException exception)
        {
            logger.LogDebug(exception, "Clipboard was temporarily unavailable.");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogDebug(exception, "Clipboard text capture failed.");
        }
    }

    private ClipboardItem CreateTextItem(string text)
    {
        var contentType = contentTypeDetector.Detect(text);
        var title = CreateTitle(text, contentType);
        var preview = text.ReplaceLineEndings(" ");
        if (preview.Length > 180)
        {
            preview = string.Concat(preview.AsSpan(0, 177), "...");
        }

        var now = DateTimeOffset.Now;

        return new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = ClipboardContentHasher.ComputeHash(text),
            Title = title,
            PreviewText = preview,
            Content = text,
            ContentType = contentType,
            SourceAppName = "Clipboard",
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
            logger.LogDebug("Unable to unregister clipboard listener. Win32 error: {Error}", error);
        }

        _messageSource.RemoveHook(WndProc);
        _messageSource.Dispose();
        _messageSource = null;
        IsCapturing = false;
    }
}
