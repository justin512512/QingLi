using System.Runtime.InteropServices;
using QingLi.Windows.Interop;
using FormsScreen = System.Windows.Forms.Screen;

namespace QingLi.Windows.Views;

internal static class CalendarPopupMonitorDpi
{
    private const int MonitorDpiTypeEffective = 0;

    internal static CalendarPopupPhysicalScreen GetForScreen(FormsScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var bounds = screen.Bounds;
        var monitor = User32.MonitorFromPoint(
            new User32.NativePoint(
                bounds.Left + bounds.Width / 2,
                bounds.Top + bounds.Height / 2),
            User32.MonitorDefaultToNearest);
        var (dpiX, dpiY) = GetEffectiveDpi(monitor);
        var monitorInfo = new User32.MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<User32.MonitorInfo>()
        };
        if (monitor != nint.Zero && User32.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return new CalendarPopupPhysicalScreen(
                screen.DeviceName,
                monitorInfo.MonitorBounds.ToRect(),
                monitorInfo.WorkArea.ToRect(),
                dpiX,
                dpiY);
        }

        return new CalendarPopupPhysicalScreen(
            screen.DeviceName,
            ToRect(bounds),
            ToRect(screen.WorkingArea),
            dpiX,
            dpiY);
    }

    private static (double DpiX, double DpiY) GetEffectiveDpi(nint monitor)
    {
        try
        {
            if (monitor != nint.Zero
                && GetDpiForMonitor(
                    monitor,
                    MonitorDpiTypeEffective,
                    out var dpiX,
                    out var dpiY) == 0
                && dpiX > 0
                && dpiY > 0)
            {
                return (dpiX, dpiY);
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        var systemDpi = User32.GetDpiForSystem();
        return systemDpi > 0 ? (systemDpi, systemDpi) : (96d, 96d);
    }

    private static System.Windows.Rect ToRect(System.Drawing.Rectangle bounds) =>
        new(bounds.Left, bounds.Top, bounds.Width, bounds.Height);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
