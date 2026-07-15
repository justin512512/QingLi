using System.Windows;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace QingLi.Windows.Views;

public sealed record CalendarPopupPhysicalScreen(
    string DeviceName,
    Rect Bounds,
    Rect WorkArea,
    double DpiX,
    double DpiY);

internal static class CalendarPopupScreenGeometry
{
    public static Rect PlaceNearCursor(
        IReadOnlyList<CalendarPopupPhysicalScreen> screens,
        WpfPoint physicalCursor,
        WpfSize popupSizeInDips)
    {
        Validate(screens);
        if (!IsFinite(physicalCursor)
            || !double.IsFinite(popupSizeInDips.Width)
            || !double.IsFinite(popupSizeInDips.Height)
            || popupSizeInDips.Width <= 0
            || popupSizeInDips.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(popupSizeInDips));
        }

        var screen = SelectForPoint(screens, physicalCursor);
        var scaleX = screen.DpiX / 96d;
        var scaleY = screen.DpiY / 96d;
        var width = popupSizeInDips.Width * scaleX;
        var height = popupSizeInDips.Height * scaleY;
        var workArea = screen.WorkArea;

        double left;
        double top;
        if (physicalCursor.Y >= workArea.Bottom)
        {
            left = physicalCursor.X - width / 2d;
            top = workArea.Bottom - height;
        }
        else if (physicalCursor.Y <= workArea.Top)
        {
            left = physicalCursor.X - width / 2d;
            top = workArea.Top;
        }
        else if (physicalCursor.X <= workArea.Left)
        {
            left = workArea.Left;
            top = physicalCursor.Y - height / 2d;
        }
        else if (physicalCursor.X >= workArea.Right)
        {
            left = workArea.Right - width;
            top = physicalCursor.Y - height / 2d;
        }
        else
        {
            left = physicalCursor.X - width / 2d;
            top = physicalCursor.Y - height;
        }

        left = width <= workArea.Width
            ? Math.Clamp(left, workArea.Left, workArea.Right - width)
            : workArea.Left;
        top = height <= workArea.Height
            ? Math.Clamp(top, workArea.Top, workArea.Bottom - height)
            : workArea.Top;
        return new Rect(left, top, width, height);
    }

    public static CalendarPopupLayout ToPersistedLayout(
        Rect physicalBounds,
        IReadOnlyList<CalendarPopupPhysicalScreen> screens)
    {
        Validate(screens);
        if (!IsValid(physicalBounds))
        {
            throw new ArgumentOutOfRangeException(nameof(physicalBounds));
        }

        var screen = SelectForBounds(screens, physicalBounds);
        var scaleX = screen.DpiX / 96d;
        var scaleY = screen.DpiY / 96d;
        return new CalendarPopupLayout(
            (physicalBounds.Left - screen.Bounds.Left) / scaleX,
            (physicalBounds.Top - screen.Bounds.Top) / scaleY,
            physicalBounds.Width / scaleX,
            physicalBounds.Height / scaleY,
            true,
            screen.DeviceName);
    }

    public static Rect RestoreSavedLayout(
        CalendarPopupLayout layout,
        IReadOnlyList<CalendarPopupPhysicalScreen> screens,
        WpfSize minimumSizeInDips,
        double visibleDragHeightInDips)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Validate(screens);
        if (string.IsNullOrWhiteSpace(layout.MonitorDeviceName)
            || !double.IsFinite(layout.Left)
            || !double.IsFinite(layout.Top)
            || !double.IsFinite(layout.Width)
            || !double.IsFinite(layout.Height)
            || layout.Width < minimumSizeInDips.Width
            || layout.Height < minimumSizeInDips.Height
            || !double.IsFinite(visibleDragHeightInDips)
            || visibleDragHeightInDips <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layout));
        }

        var screen = screens.FirstOrDefault(candidate => string.Equals(
            candidate.DeviceName,
            layout.MonitorDeviceName,
            StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentOutOfRangeException(nameof(layout));
        var scaleX = screen.DpiX / 96d;
        var scaleY = screen.DpiY / 96d;
        var saved = new Rect(
            screen.Bounds.Left + layout.Left * scaleX,
            screen.Bounds.Top + layout.Top * scaleY,
            layout.Width * scaleX,
            layout.Height * scaleY);
        return ConstrainPhysical(
            saved,
            screen.WorkArea,
            new WpfSize(minimumSizeInDips.Width * scaleX, minimumSizeInDips.Height * scaleY),
            visibleDragHeightInDips * scaleY);
    }

    private static Rect ConstrainPhysical(
        Rect saved,
        Rect workArea,
        WpfSize minimumSize,
        double visibleDragHeight)
    {
        var width = Math.Min(
            Math.Max(saved.Width, minimumSize.Width),
            Math.Max(workArea.Width, minimumSize.Width));
        var height = Math.Min(
            Math.Max(saved.Height, minimumSize.Height),
            Math.Max(workArea.Height, minimumSize.Height));
        var left = width <= workArea.Width
            ? Math.Clamp(saved.Left, workArea.Left, workArea.Right - width)
            : Math.Clamp(saved.Left, workArea.Right - width, workArea.Left);
        var top = height <= workArea.Height
            ? Math.Clamp(saved.Top, workArea.Top, workArea.Bottom - height)
            : Math.Clamp(
                saved.Top,
                workArea.Top,
                workArea.Bottom - Math.Min(visibleDragHeight, workArea.Height));
        return new Rect(left, top, width, height);
    }

    private static CalendarPopupPhysicalScreen SelectForBounds(
        IReadOnlyList<CalendarPopupPhysicalScreen> screens,
        Rect bounds)
    {
        return screens
            .OrderByDescending(screen => IntersectionArea(screen.Bounds, bounds))
            .ThenBy(screen => DistanceSquared(screen.Bounds, new WpfPoint(
                bounds.Left + bounds.Width / 2d,
                bounds.Top + bounds.Height / 2d)))
            .First();
    }

    private static CalendarPopupPhysicalScreen SelectForPoint(
        IReadOnlyList<CalendarPopupPhysicalScreen> screens,
        WpfPoint point) =>
        screens.FirstOrDefault(candidate => Contains(candidate.Bounds, point))
        ?? screens.MinBy(candidate => DistanceSquared(candidate.Bounds, point))!;

    private static double IntersectionArea(Rect first, Rect second)
    {
        var width = Math.Max(0d, Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left));
        var height = Math.Max(0d, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top));
        return width * height;
    }

    private static bool IsFinite(WpfPoint point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool Contains(Rect bounds, WpfPoint point) =>
        point.X >= bounds.Left
        && point.X < bounds.Right
        && point.Y >= bounds.Top
        && point.Y < bounds.Bottom;

    private static double DistanceSquared(Rect bounds, WpfPoint point)
    {
        var deltaX = point.X < bounds.Left
            ? bounds.Left - point.X
            : point.X >= bounds.Right
                ? point.X - bounds.Right
                : 0d;
        var deltaY = point.Y < bounds.Top
            ? bounds.Top - point.Y
            : point.Y >= bounds.Bottom
                ? point.Y - bounds.Bottom
                : 0d;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static void Validate(IReadOnlyList<CalendarPopupPhysicalScreen> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (screens.Count == 0
            || screens.Any(screen =>
                !IsValid(screen.Bounds)
                || !IsValid(screen.WorkArea)
                || string.IsNullOrWhiteSpace(screen.DeviceName)
                || !double.IsFinite(screen.DpiX)
                || !double.IsFinite(screen.DpiY)
                || screen.DpiX <= 0
                || screen.DpiY <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(screens));
        }
    }

    private static bool IsValid(Rect bounds) =>
        !bounds.IsEmpty
        && double.IsFinite(bounds.Left)
        && double.IsFinite(bounds.Top)
        && double.IsFinite(bounds.Width)
        && double.IsFinite(bounds.Height)
        && bounds.Width > 0
        && bounds.Height > 0;
}
