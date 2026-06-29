using System.Runtime.InteropServices;
using System.Windows;

namespace QingLi.Windows.Interop;

internal static class User32
{
    internal const uint MonitorDefaultToNull = 0x00000000;
    internal const uint MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativePoint(int x, int y)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        internal readonly Rect ToRect() =>
            new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal NativeRect MonitorBounds;
        internal NativeRect WorkArea;
        internal uint Flags;
    }

    [DllImport("user32.dll", EntryPoint = "MonitorFromRect")]
    internal static extern nint MonitorFromRect(
        ref NativeRect rect,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "MonitorFromPoint")]
    internal static extern nint MonitorFromPoint(
        NativePoint point,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "MonitorFromWindow")]
    internal static extern nint MonitorFromWindow(
        nint windowHandle,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(
        nint monitor,
        ref MonitorInfo monitorInfo);

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    internal static extern nint FindWindow(string className, string? windowName);

    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    internal static extern nint FindWindowEx(
        nint parentWindow,
        nint childAfter,
        string className,
        string? windowName);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(
        nint windowHandle,
        out NativeRect bounds);

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    internal static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetDpiForSystem")]
    internal static extern uint GetDpiForSystem();
}
