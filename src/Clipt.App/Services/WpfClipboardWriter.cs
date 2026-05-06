using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Collections.Specialized;
using System.Windows.Media.Imaging;
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
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Image items in Auto mode: attempt to write the cached
            // preview bitmap before falling back to metadata text.
            if (item.ContentType == ContentType.Image && options.PasteMode == PasteMode.Auto)
            {
                if (await TryWriteImageWithRetryAsync(item, cancellationToken))
                    return;
                // Fall through to plain text below.
            }

            var text = item.Content;
            if (string.IsNullOrEmpty(text))
            {
                logger.LogDebug("Clipboard item {Id} has no text content to write.", item.Id);
                return;
            }

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

    // ── Image write ──────────────────────────────────────────────────

    /// <summary>
    /// Attempts to write a cached image preview to the system clipboard.
    /// Returns <c>true</c> on success. Returns <c>false</c> when the
    /// preview file is missing, cannot be loaded, or the write fails
    /// for a non-busy reason (caller should fall back to text).
    /// </summary>
    private async Task<bool> TryWriteImageWithRetryAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        var localPath = FilePathDisplayHelper.ConvertFileUriToLocalPath(item.ImageUri);
        if (localPath is null || !File.Exists(localPath))
        {
            logger.LogDebug(
                "Image item {Id} has no cached preview at {Path}, falling back to text.",
                item.Id,
                localPath ?? "<null>");
            return false;
        }

        BitmapSource bitmap;
        try
        {
            bitmap = LoadImageFromFile(localPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to load image {Path} for item {Id}, falling back to text.",
                localPath,
                item.Id);
            return false;
        }

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool busy;
            try
            {
                busy = await TryWriteImageOnDispatcherAsync(bitmap);
            }
            catch (Exception ex) when (!IsClipboardBusy(ex))
            {
                logger.LogWarning(
                    ex,
                    "Image clipboard write failed for item {Id}, falling back to text.",
                    item.Id);
                return false;
            }

            if (!busy)
            {
                logger.LogInformation(
                    "Wrote image {Width}x{Height} from {Path} for item {Id} to clipboard (attempt {Attempt}).",
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    localPath,
                    item.Id,
                    attempt + 1);
                return true;
            }

            if (attempt == MaxRetries - 1) break;

            var backoff = RetryBackoffMs[attempt];
            logger.LogDebug(
                "Clipboard busy during image write, retrying in {BackoffMs}ms (attempt {Attempt}/{MaxRetries}).",
                backoff,
                attempt + 1,
                MaxRetries);

            await Task.Delay(backoff, cancellationToken);
        }

        logger.LogWarning(
            "Failed to write image to clipboard after {MaxRetries} attempts for item {Id}, falling back to text.",
            MaxRetries,
            item.Id);
        return false;
    }

    /// <summary>
    /// Invokes <see cref="Clipboard.SetImage"/> on the WPF dispatcher
    /// with internal exception capture so the retry loop can handle
    /// clipboard-busy errors.
    /// </summary>
    /// <returns>true if the clipboard was busy and the caller should retry.</returns>
    private async Task<bool> TryWriteImageOnDispatcherAsync(BitmapSource bitmap)
    {
        Exception? captured = null;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                Clipboard.SetImage(bitmap);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is null) return false;

        if (IsClipboardBusy(captured)) return true;

        // Non-busy failure — rethrow so the caller can log and fall back.
        throw captured;
    }

    /// <summary>
    /// Loads a <see cref="BitmapSource"/> from a local file path.
    /// The bitmap is fully decoded and frozen before returning.
    /// </summary>
    private static BitmapSource LoadImageFromFile(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        if (bitmap.CanFreeze)
        {
            bitmap.Freeze();
        }

        return bitmap;
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

                if (item.ContentType == ContentType.File && item.FilePaths.Count > 0)
                {
                    var fileDropList = new StringCollection();
                    fileDropList.AddRange(item.FilePaths.ToArray());
                    dataObject.SetFileDropList(fileDropList);
                }

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
