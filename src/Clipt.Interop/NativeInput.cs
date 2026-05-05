using System.Runtime.InteropServices;

namespace Clipt.Interop;

public static partial class NativeMethods
{
    public const int InputKeyboard = 1;
    public const int KeyeventfKeyup = 0x0002;
    public const int KeyeventfExtendedKey = 0x0001;
    public const int KeyeventfScancode = 0x0008;
    public const ushort VkControl = 0x11;
    public const ushort VkV = 0x56;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(
        uint cInputs,
        [MarshalAs(UnmanagedType.LPArray), In] Input[] pInputs,
        int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(nint hWnd);

    /// <summary>
    /// Waits until the target window's input queue is idle.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(
        nint idAttach,
        nint idAttachTo,
        [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}

[StructLayout(LayoutKind.Sequential)]
public struct Input
{
    public uint Type;
    public InputUnion U;
}

[StructLayout(LayoutKind.Explicit)]
public struct InputUnion
{
    [FieldOffset(0)]
    public MouseInput Mi;

    [FieldOffset(0)]
    public KeyboardInput Ki;

    [FieldOffset(0)]
    public HardwareInput Hi;
}

[StructLayout(LayoutKind.Sequential)]
public struct MouseInput
{
    public int Dx;
    public int Dy;
    public uint MouseData;
    public uint DwFlags;
    public uint Time;
    public nint DwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct KeyboardInput
{
    public ushort WVk;
    public ushort WScan;
    public uint DwFlags;
    public uint Time;
    public nint DwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct HardwareInput
{
    public uint UMsg;
    public ushort WParamL;
    public ushort WParamH;
}
