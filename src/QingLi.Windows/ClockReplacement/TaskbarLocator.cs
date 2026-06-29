using System.Runtime.InteropServices;
using System.Windows;
using QingLi.Windows.Interop;
using ScreenPoint = System.Windows.Point;

namespace QingLi.Windows.ClockReplacement;

public interface ITaskbarNativeApi
{
    bool TryGetPrimaryTaskbar(out TaskbarNativeData data);

    bool TryGetTaskbarForPoint(ScreenPoint screenPoint, out TaskbarNativeData data);
}

public sealed class TaskbarLocator
{
    private const double GeometryTolerance = 1d;
    private const uint MinimumDpi = 48;
    private const uint MaximumDpi = 768;

    private readonly ITaskbarNativeApi _nativeApi;

    public TaskbarLocator()
        : this(new WindowsTaskbarNativeApi())
    {
    }

    public TaskbarLocator(ITaskbarNativeApi nativeApi)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
    }

    public TaskbarGeometry? GetPrimary() =>
        TryGetValidatedData(out var data) ? ToGeometry(data) : null;

    public TaskbarGeometry? GetForPoint(ScreenPoint screenPoint)
    {
        if (!double.IsFinite(screenPoint.X) || !double.IsFinite(screenPoint.Y) ||
            !_nativeApi.TryGetTaskbarForPoint(screenPoint, out var data) ||
            !IsValidatedData(data) ||
            !data.MonitorBounds.Contains(screenPoint))
        {
            return null;
        }

        return ToGeometry(data);
    }

    private bool TryGetValidatedData(out TaskbarNativeData data)
    {
        return _nativeApi.TryGetPrimaryTaskbar(out data) && IsValidatedData(data);
    }

    private static bool IsValidatedData(TaskbarNativeData data)
    {
        var taskbar = data.Bounds;
        var monitor = data.MonitorBounds;
        if (data.Edge != TaskbarEdge.Bottom ||
            !IsFiniteRect(taskbar) ||
            !IsFiniteRect(monitor) ||
            data.Dpi is < MinimumDpi or > MaximumDpi)
        {
            return false;
        }

        var maximumPlausibleHeight = Math.Min(200d, monitor.Height * 0.25d);
        return taskbar.Height >= 16d &&
               taskbar.Height <= maximumPlausibleHeight &&
               taskbar.Width >= 320d &&
               NearlyEqual(taskbar.Left, monitor.Left) &&
               NearlyEqual(taskbar.Right, monitor.Right) &&
               NearlyEqual(taskbar.Bottom, monitor.Bottom) &&
               taskbar.Top >= monitor.Top - GeometryTolerance;
    }

    private static TaskbarGeometry ToGeometry(TaskbarNativeData data) =>
        new(data.Bounds, data.Edge, data.Dpi / 96d);

    private static bool NearlyEqual(double left, double right) =>
        Math.Abs(left - right) <= GeometryTolerance;

    private static bool IsFiniteRect(Rect rect) =>
        !rect.IsEmpty &&
        double.IsFinite(rect.X) &&
        double.IsFinite(rect.Y) &&
        double.IsFinite(rect.Width) &&
        double.IsFinite(rect.Height) &&
        rect.Width > 0 &&
        rect.Height > 0;

    private sealed class WindowsTaskbarNativeApi : ITaskbarNativeApi
    {
        public bool TryGetPrimaryTaskbar(out TaskbarNativeData data)
        {
            data = default;

            try
            {
                var appBarData = new Shell32.AppBarData
                {
                    Size = (uint)Marshal.SizeOf<Shell32.AppBarData>()
                };

                if (Shell32.SHAppBarMessage(Shell32.AbmGetTaskbarPos, ref appBarData) == 0)
                {
                    return false;
                }

                var taskbarRect = appBarData.Bounds;
                var monitor = User32.MonitorFromRect(
                    ref taskbarRect,
                    User32.MonitorDefaultToNearest);
                if (monitor == 0)
                {
                    return false;
                }

                var monitorInfo = new User32.MonitorInfo
                {
                    Size = (uint)Marshal.SizeOf<User32.MonitorInfo>()
                };
                if (!User32.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    return false;
                }

                var taskbarWindow = User32.FindWindow("Shell_TrayWnd", null);
                if (taskbarWindow == 0)
                {
                    return false;
                }

                var dpi = User32.GetDpiForWindow(taskbarWindow);

                data = new TaskbarNativeData(
                    appBarData.Bounds.ToRect(),
                    (TaskbarEdge)appBarData.Edge,
                    monitorInfo.MonitorBounds.ToRect(),
                    dpi);
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        public bool TryGetTaskbarForPoint(
            ScreenPoint screenPoint,
            out TaskbarNativeData data)
        {
            data = default;
            if (!TryConvertPoint(screenPoint, out var nativePoint))
            {
                return false;
            }

            try
            {
                var targetMonitor = User32.MonitorFromPoint(
                    nativePoint,
                    User32.MonitorDefaultToNull);
                if (targetMonitor == 0)
                {
                    return false;
                }

                var primaryTaskbar = User32.FindWindow("Shell_TrayWnd", null);
                if (primaryTaskbar != 0 &&
                    User32.MonitorFromWindow(primaryTaskbar, User32.MonitorDefaultToNull) == targetMonitor)
                {
                    return TryGetPrimaryTaskbar(out data);
                }

                nint taskbar = 0;
                while (true)
                {
                    taskbar = User32.FindWindowEx(
                        0,
                        taskbar,
                        "Shell_SecondaryTrayWnd",
                        null);
                    if (taskbar == 0)
                    {
                        return false;
                    }

                    if (User32.MonitorFromWindow(taskbar, User32.MonitorDefaultToNull) == targetMonitor)
                    {
                        return TryGetWindowTaskbar(taskbar, targetMonitor, out data);
                    }
                }
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        private static bool TryGetWindowTaskbar(
            nint taskbarWindow,
            nint monitor,
            out TaskbarNativeData data)
        {
            data = default;
            if (!User32.GetWindowRect(taskbarWindow, out var taskbarBounds))
            {
                return false;
            }

            var monitorInfo = new User32.MonitorInfo
            {
                Size = (uint)Marshal.SizeOf<User32.MonitorInfo>()
            };
            if (!User32.GetMonitorInfo(monitor, ref monitorInfo) ||
                !TryGetEdge(taskbarBounds, monitorInfo.MonitorBounds, out var edge))
            {
                return false;
            }

            var dpi = User32.GetDpiForWindow(taskbarWindow);
            if (dpi == 0)
            {
                return false;
            }

            data = new TaskbarNativeData(
                taskbarBounds.ToRect(),
                edge,
                monitorInfo.MonitorBounds.ToRect(),
                dpi);
            return true;
        }

        private static bool TryGetEdge(
            User32.NativeRect taskbar,
            User32.NativeRect monitor,
            out TaskbarEdge edge)
        {
            const int tolerance = 1;
            var spansWidth = Math.Abs(taskbar.Left - monitor.Left) <= tolerance &&
                             Math.Abs(taskbar.Right - monitor.Right) <= tolerance;
            var spansHeight = Math.Abs(taskbar.Top - monitor.Top) <= tolerance &&
                              Math.Abs(taskbar.Bottom - monitor.Bottom) <= tolerance;

            if (spansWidth && Math.Abs(taskbar.Bottom - monitor.Bottom) <= tolerance)
            {
                edge = TaskbarEdge.Bottom;
                return true;
            }

            if (spansWidth && Math.Abs(taskbar.Top - monitor.Top) <= tolerance)
            {
                edge = TaskbarEdge.Top;
                return true;
            }

            if (spansHeight && Math.Abs(taskbar.Left - monitor.Left) <= tolerance)
            {
                edge = TaskbarEdge.Left;
                return true;
            }

            if (spansHeight && Math.Abs(taskbar.Right - monitor.Right) <= tolerance)
            {
                edge = TaskbarEdge.Right;
                return true;
            }

            edge = default;
            return false;
        }

        private static bool TryConvertPoint(
            ScreenPoint screenPoint,
            out User32.NativePoint nativePoint)
        {
            if (!double.IsFinite(screenPoint.X) || !double.IsFinite(screenPoint.Y) ||
                screenPoint.X < int.MinValue || screenPoint.X > int.MaxValue ||
                screenPoint.Y < int.MinValue || screenPoint.Y > int.MaxValue)
            {
                nativePoint = default;
                return false;
            }

            nativePoint = new User32.NativePoint(
                (int)Math.Round(screenPoint.X, MidpointRounding.AwayFromZero),
                (int)Math.Round(screenPoint.Y, MidpointRounding.AwayFromZero));
            return true;
        }
    }
}
