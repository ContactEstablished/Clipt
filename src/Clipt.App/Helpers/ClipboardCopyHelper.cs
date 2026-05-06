using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clipt.App.Helpers;

public static class ClipboardCopyHelper
{
    private static readonly ILogger _logger =
        NullLoggerFactory.Instance.CreateLogger(nameof(ClipboardCopyHelper));

    private const int CopyMaxRetries = 4;
    private static readonly int[] CopyRetryBackoffMs = [25, 75, 200, 500];

    /// <summary>
    /// Copies <paramref name="text"/> as Unicode text to the system clipboard
    /// with retry handling for CLIPBRD_E_CANT_OPEN (0x800401D0).
    /// <paramref name="description"/> is used in log messages only.
    /// </summary>
    public static async Task CopyTextWithRetryAsync(string text, string description)
    {
        for (var attempt = 0; attempt < CopyMaxRetries; attempt++)
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

            if (captured is null)
            {
                _logger.LogDebug(
                    "Copied {Description} to clipboard (attempt {Attempt}).",
                    description,
                    attempt + 1);
                return;
            }

            if (!IsClipboardBusy(captured))
            {
                _logger.LogError(
                    captured,
                    "Failed to copy {Description} to clipboard on attempt {Attempt}.",
                    description,
                    attempt + 1);
                throw captured;
            }

            if (attempt == CopyMaxRetries - 1)
            {
                _logger.LogError(
                    "Clipboard busy after {MaxRetries} attempts for {Description}.",
                    CopyMaxRetries,
                    description);
                throw captured;
            }

            _logger.LogDebug(
                "Clipboard busy during {Description} copy, retrying in {BackoffMs}ms (attempt {Attempt}/{MaxRetries}).",
                description,
                CopyRetryBackoffMs[attempt],
                attempt + 1,
                CopyMaxRetries);

            await Task.Delay(CopyRetryBackoffMs[attempt]);
        }
    }

    private static bool IsClipboardBusy(Exception ex) =>
        ex is ExternalException
        && unchecked((uint)ex.HResult) == 0x800401D0;
}
