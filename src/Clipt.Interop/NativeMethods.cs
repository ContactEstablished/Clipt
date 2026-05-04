using System.Runtime.InteropServices;

namespace Clipt.Interop;

public static partial class NativeMethods
{
    public const int DwmwaWindowCornerPreference = 33;
    public const int DwmwaSystemBackdropType = 38;

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(
        nint hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
