using System.Runtime.InteropServices;

namespace QingLi.Windows.Interop;

internal static class Shell32
{
    internal const uint AbmGetTaskbarPos = 0x00000005;

    [StructLayout(LayoutKind.Sequential)]
    internal struct AppBarData
    {
        internal uint Size;
        internal nint WindowHandle;
        internal uint CallbackMessage;
        internal uint Edge;
        internal User32.NativeRect Bounds;
        internal nint Parameter;
    }

    [DllImport("shell32.dll", EntryPoint = "SHAppBarMessage", SetLastError = true)]
    internal static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
}
