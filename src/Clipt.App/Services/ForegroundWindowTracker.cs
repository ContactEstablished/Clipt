using Clipt.Interop;

namespace Clipt.App.Services;

/// <summary>
/// Captures the foreground window handle before Clipt takes focus.
/// Call <see cref="Capture"/> immediately before showing/activating the
/// main window, then read <see cref="PreviousForegroundWindow"/> later for
/// the window that had focus before Clipt appeared.
/// </summary>
public sealed class ForegroundWindowTracker
{
    /// <summary>
    /// The handle of the window that was in the foreground before Clipt
    /// was summoned. <c>0</c> when never captured.
    /// </summary>
    public nint PreviousForegroundWindow { get; private set; }

    /// <summary>
    /// Records the current foreground window handle. Call this before
    /// showing or activating Clipt's window.
    /// </summary>
    public void Capture()
    {
        PreviousForegroundWindow = NativeMethods.GetForegroundWindow();
    }

    /// <summary>
    /// Resets <see cref="PreviousForegroundWindow"/> to 0. Call this when
    /// the captured handle is not usable (e.g. it belongs to Clipt itself).
    /// </summary>
    public void Clear()
    {
        PreviousForegroundWindow = 0;
    }
}
