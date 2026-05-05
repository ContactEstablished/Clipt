namespace Clipt.App.Services;

public interface IInputSimulator
{
    /// <summary>
    /// Sends Ctrl+V to the target window using <c>SendInput</c>.
    /// Returns false when the target is invalid and the paste was not attempted.
    /// </summary>
    Task<bool> SendPasteAsync(nint targetHwnd, CancellationToken cancellationToken);
}
