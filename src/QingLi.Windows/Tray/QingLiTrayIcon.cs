using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace QingLi.Windows.Tray;

public static class QingLiTrayIcon
{
    private static readonly int[] SupportedSizes = [16, 20, 24, 32, 40, 48];

    private static readonly Uri ResourceUri = new(
        "/QingLi.Windows;component/Assets/Brand/QingLi.ico",
        UriKind.Relative);

    public static Icon Create() => Create(SelectIconSize(GetTrayDpi()));

    internal static Icon Create(int size) => Create(size, OpenResourceStream);

    internal static Icon Create(int size, Func<Stream> openResourceStream)
    {
        using var stream = openResourceStream();
        using var icon = new Icon(stream, size, size);
        return (Icon)icon.Clone();
    }

    internal static int SelectIconSize(int dpi)
    {
        var targetSize = 16d * (dpi > 0 ? dpi : 96) / 96d;
        return SupportedSizes.MinBy(size => Math.Abs(size - targetSize));
    }

    private static int GetTrayDpi()
    {
        try
        {
            var taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
            {
                var taskbarDpi = GetDpiForWindow(taskbar);
                if (taskbarDpi > 0)
                {
                    return checked((int)taskbarDpi);
                }
            }

            var systemDpi = GetDpiForSystem();
            if (systemDpi > 0)
            {
                return checked((int)systemDpi);
            }
        }
        catch (Exception)
        {
            // Older or restricted Windows environments may not expose these APIs.
        }

        return 96;
    }

    private static Stream OpenResourceStream() =>
        System.Windows.Application.GetResourceStream(ResourceUri)?.Stream
        ?? throw new InvalidOperationException($"WPF resource '{ResourceUri}' was not found.");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
