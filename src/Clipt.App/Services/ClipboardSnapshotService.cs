using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

public sealed class ClipboardSnapshotService(ILogger<ClipboardSnapshotService> logger)
{
    private const int MaxRetries = 4;

    private static readonly int[] RetryBackoffMs = [25, 50, 100, 200];

    public async Task<ClipboardSnapshot?> CaptureAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ClipboardSnapshot? snapshot = null;
            Exception? captured = null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    snapshot = TryCaptureOnDispatcher();
                }
                catch (Exception exception)
                {
                    captured = exception;
                }
            });

            if (captured is null)
            {
                return snapshot;
            }

            if (!WpfClipboardWriter.IsClipboardBusy(captured))
            {
                logger.LogDebug(captured, "Failed to capture clipboard snapshot.");
                return null;
            }

            if (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryBackoffMs[attempt], cancellationToken);
            }
        }

        logger.LogDebug("Clipboard remained busy while capturing snapshot.");
        return null;
    }

    public async Task RestoreAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Formats.Count == 0)
        {
            return;
        }

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Exception? captured = null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    RestoreOnDispatcher(snapshot);
                }
                catch (Exception exception)
                {
                    captured = exception;
                }
            });

            if (captured is null)
            {
                logger.LogDebug("Restored previous clipboard snapshot with {Count} format(s).", snapshot.Formats.Count);
                return;
            }

            if (!WpfClipboardWriter.IsClipboardBusy(captured))
            {
                logger.LogWarning(captured, "Failed to restore previous clipboard snapshot.");
                return;
            }

            if (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryBackoffMs[attempt], cancellationToken);
            }
        }

        logger.LogWarning("Clipboard remained busy; previous clipboard snapshot was not restored.");
    }

    private static ClipboardSnapshot? TryCaptureOnDispatcher()
    {
        var dataObject = Clipboard.GetDataObject();
        if (dataObject is null)
        {
            return null;
        }

        var formats = new List<ClipboardSnapshotFormat>();
        foreach (var format in dataObject.GetFormats(autoConvert: false))
        {
            try
            {
                var data = dataObject.GetData(format, autoConvert: false);
                if (data is null)
                {
                    continue;
                }

                formats.Add(new ClipboardSnapshotFormat(format, CloneKnownClipboardData(data)));
            }
            catch (ExternalException)
            {
                // Format became unavailable mid-read, or a custom owner-rendered
                // format could not be materialized; preserve the rest.
            }
            catch (InvalidOperationException)
            {
            }
        }

        return formats.Count == 0 ? null : new ClipboardSnapshot(formats);
    }

    private static object CloneKnownClipboardData(object data)
    {
        if (data is string text)
        {
            return text;
        }

        if (data is string[] paths)
        {
            return paths.ToArray();
        }

        if (data is StringCollection collection)
        {
            var copy = new StringCollection();
            copy.AddRange(collection.Cast<string>().ToArray());
            return copy;
        }

        if (data is BitmapSource bitmap)
        {
            if (bitmap.CanFreeze && !bitmap.IsFrozen)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }

        if (data is byte[] bytes)
        {
            return bytes.ToArray();
        }

        return data;
    }

    private static void RestoreOnDispatcher(ClipboardSnapshot snapshot)
    {
        var dataObject = new DataObject();
        foreach (var format in snapshot.Formats)
        {
            dataObject.SetData(format.Format, format.Data, autoConvert: false);
        }

        Clipboard.SetDataObject(dataObject, copy: true);
    }
}

public sealed record ClipboardSnapshot(IReadOnlyList<ClipboardSnapshotFormat> Formats);

public sealed record ClipboardSnapshotFormat(string Format, object Data);
