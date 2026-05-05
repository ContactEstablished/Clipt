using System.ComponentModel;
using System.Diagnostics;
using Clipt.Interop;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

/// <summary>
/// Resolves source application metadata from the foreground window using Win32 APIs.
/// </summary>
public sealed class WindowsSourceAppResolver(
    ForegroundWindowTracker foregroundWindowTracker,
    ILogger<WindowsSourceAppResolver> logger) : ISourceAppResolver
{
    public SourceAppInfo Resolve()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == 0)
            {
                logger.LogDebug("GetForegroundWindow returned null handle.");
                return SourceAppInfo.Unknown;
            }

            var processId = GetProcessIdFromWindow(hwnd);
            if (processId == 0)
            {
                logger.LogDebug("Unable to retrieve process id from foreground window.");
                return SourceAppInfo.Unknown;
            }

            // If Clipt itself is the foreground window, try the previously tracked window.
            using var currentProcess = Process.GetCurrentProcess();
            if (processId == currentProcess.Id)
            {
                logger.LogDebug("Foreground window belongs to Clipt itself; trying previous foreground.");
                var previousHwnd = foregroundWindowTracker.PreviousForegroundWindow;
                if (previousHwnd != 0 && previousHwnd != hwnd)
                {
                    var previousPid = GetProcessIdFromWindow(previousHwnd);
                    if (previousPid != 0 && previousPid != currentProcess.Id)
                    {
                        return ResolveFromProcessId(previousPid);
                    }
                }

                return SourceAppInfo.Unknown;
            }

            return ResolveFromProcessId(processId);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Source app resolution failed; returning Unknown.");
            return SourceAppInfo.Unknown;
        }
    }

    private static uint GetProcessIdFromWindow(nint hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return processId;
    }

    private SourceAppInfo ResolveFromProcessId(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var name = GetFriendlyProcessName(process);
            var path = GetSafeProcessPath(process);

            return new SourceAppInfo { Name = name, Path = path };
        }
        catch (ArgumentException)
        {
            logger.LogDebug("Process {ProcessId} no longer exists.", processId);
            return SourceAppInfo.Unknown;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 5) // Access denied
        {
            logger.LogDebug(exception, "Access denied when opening process {ProcessId}.", processId);
            return SourceAppInfo.Unknown;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unexpected error resolving process {ProcessId}.", processId);
            return SourceAppInfo.Unknown;
        }
    }

    private static string GetFriendlyProcessName(Process process)
    {
        try
        {
            return process.MainModule?.FileVersionInfo.FileDescription
                ?? process.ProcessName;
        }
        catch
        {
            try
            {
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    private static string? GetSafeProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
