using System.Runtime.InteropServices;
using System.Windows;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

public sealed class WpfClipboardWriter(ILogger<WpfClipboardWriter> logger) : IClipboardWriter
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(10);

    public async Task WriteAsync(ClipboardItem item, ClipboardWriteOptions options, CancellationToken cancellationToken)
    {
        var text = item.Content;
        if (string.IsNullOrEmpty(text))
        {
            logger.LogDebug("Clipboard item {Id} has no text content to write.", item.Id);
            return;
        }

        // All paste modes currently write Unicode text. Rich format
        // preservation (HTML, RTF, etc.) is deferred to a future phase.
        await WriteTextWithRetryAsync(text, cancellationToken);
    }

    private async Task WriteTextWithRetryAsync(string text, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Clipboard APIs must run on the WPF UI thread.
                await Application.Current.Dispatcher.InvokeAsync(
                    () => Clipboard.SetText(text, TextDataFormat.UnicodeText));

                logger.LogDebug(
                    "Wrote {ByteCount} bytes of Unicode text to the clipboard (attempt {Attempt}).",
                    text.Length * 2,
                    attempt + 1);

                return;
            }
            catch (ExternalException exception) when ((uint)exception.HResult == 0x800401D0)
            {
                // CLIPBRD_E_CANT_OPEN — another process owns the clipboard.
                if (attempt == MaxRetries - 1)
                {
                    logger.LogError(exception, "Failed to write to clipboard after {MaxRetries} attempts.", MaxRetries);
                    throw;
                }

                var backoff = InitialBackoff * Math.Pow(2, attempt);
                logger.LogDebug(
                    exception,
                    "Clipboard busy, retrying in {BackoffMs}ms (attempt {Attempt}).",
                    backoff.TotalMilliseconds,
                    attempt + 1);

                await Task.Delay(backoff, cancellationToken);
            }
        }
    }
}
