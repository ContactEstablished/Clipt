namespace Clipt.App.Services;

/// <summary>
/// Resolves the source application that owns the current foreground window,
/// typically called at the moment of a clipboard update.
/// </summary>
public interface ISourceAppResolver
{
    /// <summary>
    /// Returns the source app info for the current foreground window.
    /// Must not throw — return <see cref="SourceAppInfo.Unknown"/> on any failure.
    /// </summary>
    SourceAppInfo Resolve();
}
