using System.Windows;
using WpfPoint = System.Windows.Point;

namespace QingLi.Windows.Views;

internal sealed record CalendarPopupPhysicalScreen(
    Rect Bounds,
    Rect WorkArea,
    double DpiX,
    double DpiY);

internal sealed record CalendarPopupCursorGeometry(
    Rect WorkArea,
    WpfPoint Anchor);

internal static class CalendarPopupScreenGeometry
{
    public static IReadOnlyList<Rect> GetWorkAreasInDips(
        IReadOnlyList<CalendarPopupPhysicalScreen> screens)
    {
        Validate(screens);
        return screens.Select(screen => ToDips(screen.WorkArea, screen)).ToArray();
    }

    public static CalendarPopupCursorGeometry GetCursorGeometry(
        IReadOnlyList<CalendarPopupPhysicalScreen> screens,
        WpfPoint physicalCursor)
    {
        Validate(screens);
        if (!double.IsFinite(physicalCursor.X) || !double.IsFinite(physicalCursor.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(physicalCursor));
        }

        var screen = screens.FirstOrDefault(candidate => Contains(candidate.Bounds, physicalCursor))
            ?? screens.MinBy(candidate => DistanceSquared(candidate.Bounds, physicalCursor))!;
        return new CalendarPopupCursorGeometry(
            ToDips(screen.WorkArea, screen),
            new WpfPoint(
                physicalCursor.X / (screen.DpiX / 96d),
                physicalCursor.Y / (screen.DpiY / 96d)));
    }

    private static Rect ToDips(Rect physicalBounds, CalendarPopupPhysicalScreen screen) =>
        new(
            physicalBounds.Left / (screen.DpiX / 96d),
            physicalBounds.Top / (screen.DpiY / 96d),
            physicalBounds.Width / (screen.DpiX / 96d),
            physicalBounds.Height / (screen.DpiY / 96d));

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
