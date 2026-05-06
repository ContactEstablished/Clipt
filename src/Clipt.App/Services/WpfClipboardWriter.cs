using System.Runtime.InteropServices;
using System.Windows;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

public sealed class WpfClipboardWriter(ILogger<WpfClipboardWriter> logger) : IClipboardWriter
{
    /// <summary>
    /// Maximum retry attempts per clipboard write operation.
    /// Total worst-case backoff across 6 attempts is ~1 575 ms,
    /// keeping the UI from feeling frozen for more than a moment.
    /// </summary>
    private const int MaxRetries = 6;

    private static readonly int[] RetryBackoffMs = [25, 50, 100, 200, 400, 800];

    /// <summary>
    /// Serialises all clipboard writes so overlapping Enter/Ctrl+Enter key
    /// presses cannot race each other on the system clipboard.
    /// </summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // ── Public API ──────────────────────────────────────────────────

    public async Task WriteAsync(ClipboardItem item, ClipboardWriteOptions options, CancellationToken cancellationToken)
    {
        var text = item.Content;
        if (string.IsNullOrEmpty(text))
        {
            logger.LogDebug("Clipboard item {Id} has no text content to write.", item.Id);
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (options.PasteMode == PasteMode.PlainText || item.Formats.Count <= 1)
            {
                await WriteTextWithRetryAsync(text, cancellationToken);
                return;
            }

            await WriteMultiFormatWithRetryAsync(item, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Plain-text write ────────────────────────────────────────────

    private async Task WriteTextWithRetryAsync(string text, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var busy = await TryWriteTextOnDispatcherAsync(text);
            if (!busy)
            {
                logger.LogDebug(
                    "Wrote {ByteCount} bytes of Unicode text to clipboard (attempt {Attempt}).",
                    text.Length * 2,
                    attempt + 1);
                return;
            }

            if (attempt == MaxRetries - 1) break;

            var backoff = RetryBackoffMs[attempt];
            logger.LogDebug(
                "Clipboard busy during text write, retrying in {BackoffMs}ms (attempt {Attempt}/{MaxRetries}).",
                backoff,
                attempt + 1,
                MaxRetries);

            await Task.Delay(backoff, cancellationToken);
        }

        logger.LogError("Failed to write text to clipboard after {MaxRetries} attempts.", MaxRetries);
        throw new ExternalException("CLIPBRD_E_CANT_OPEN after max retries", unchecked((int)0x800401D0));
    }

    /// <summary>
    /// Invokes <see cref="Clipboard.SetText"/> on the WPF dispatcher
    /// with internal exception capture so the retry loop can handle
    /// clipboard-busy errors without Visual Studio breaking on a
    /// user-unhandled exception inside the dispatcher lambda.
    /// </summary>
    /// <returns>true if the clipboard was busy and the caller should retry.</returns>
    private async Task<bool> TryWriteTextOnDispatcherAsync(string text)
    {
        Exception? captured = null;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is null) return false;

        if (IsClipboardBusy(captured)) return true;

        // Non-busy failure — rethrow so the caller can log and propagate.
        throw captured;
    }

    // ── Multi-format write ──────────────────────────────────────────

    private async Task WriteMultiFormatWithRetryAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool busy;
            try
            {
                busy = await TryWriteMultiFormatOnDispatcherAsync(item);
            }
            catch (Exception ex) when (!IsClipboardBusy(ex))
            {
                // Non-busy failure: fall back to plain Unicode text so the
                // user can still paste even when rich-format write fails.
                logger.LogWarning(ex, "Multi-format clipboard write failed, falling back to plain text.");
                await WriteTextWithRetryAsync(item.Content, cancellationToken);
                return;
            }

            if (!busy)
            {
                logger.LogDebug(
                    "Wrote multi-format clipboard data (attempt {Attempt}, {FormatCount} formats).",
                    attempt + 1,
                    item.Formats.Count);
                return;
            }

            if (attempt == MaxRetries - 1) break;

            var backoff = RetryBackoffMs[attempt];
            logger.LogDebug(
                "Clipboard busy during multi-format write, retrying in {BackoffMs}ms (attempt {Attempt}/{MaxRetries}).",
                backoff,
                attempt + 1,
                MaxRetries);

            await Task.Delay(backoff, cancellationToken);
        }

        logger.LogError("Failed to write multi-format data after {MaxRetries} attempts.", MaxRetries);
        throw new ExternalException("CLIPBRD_E_CANT_OPEN after max retries", unchecked((int)0x800401D0));
    }

    /// <summary>
    /// Invokes a multi-format <see cref="Clipboard.SetDataObject"/>
    /// on the WPF dispatcher with internal exception capture.
    /// </summary>
    private async Task<bool> TryWriteMultiFormatOnDispatcherAsync(ClipboardItem item)
    {
        Exception? captured = null;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var dataObject = new DataObject();

                // UnicodeText is the primary format — set it first.
                dataObject.SetText(item.Content, TextDataFormat.UnicodeText);

                var html = FindFormatText(item, DataFormats.Html);
                if (html is not null)
                    dataObject.SetData(DataFormats.Html, html);

                var rtf = FindFormatText(item, DataFormats.Rtf);
                if (rtf is not null)
                    dataObject.SetData(DataFormats.Rtf, rtf);

                var ansiText = FindFormatText(item, DataFormats.Text);
                if (ansiText is not null)
                    dataObject.SetData(DataFormats.Text, ansiText);

                Clipboard.SetDataObject(dataObject, copy: true);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is null) return false;

        if (IsClipboardBusy(captured)) return true;

        throw captured;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the exception indicates a transient clipboard-busy
    /// error (CLIPBRD_E_CANT_OPEN, 0x800401D0).  WPF may surface this as
    /// either <see cref="ExternalException"/> or <see cref="COMException"/>;
    /// both share the same HResult.
    /// </summary>
    public static bool IsClipboardBusy(Exception exception) =>
        exception is ExternalException
        && unchecked((uint)exception.HResult) == 0x800401D0;

    /// <summary>
    /// Looks up a format's text payload by name from the item's registered formats.
    /// </summary>
    private static string? FindFormatText(ClipboardItem item, string formatName)
    {
        foreach (var f in item.Formats)
        {
            if (f.Name == formatName)
                return f.TextPayload;
        }

        return null;
    }
}
