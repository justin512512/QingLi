using System.Windows;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace QingLi.Windows.Views;

public static class CalendarPopupPlacement
{
    public static Rect Calculate(Rect workArea, WpfSize popupSize, WpfPoint anchor)
    {
        if (workArea.IsEmpty
            || !IsPositiveFinite(workArea.Width)
            || !IsPositiveFinite(workArea.Height)
            || !IsPositiveFinite(popupSize.Width)
            || !IsPositiveFinite(popupSize.Height)
            || popupSize.Width > workArea.Width
            || popupSize.Height > workArea.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(popupSize));
        }

        double left;
        double top;
        if (anchor.Y >= workArea.Bottom)
        {
            left = anchor.X - popupSize.Width / 2d;
            top = workArea.Bottom - popupSize.Height;
        }
        else if (anchor.Y <= workArea.Top)
        {
            left = anchor.X - popupSize.Width / 2d;
            top = workArea.Top;
        }
        else if (anchor.X <= workArea.Left)
        {
            left = workArea.Left;
            top = anchor.Y - popupSize.Height / 2d;
        }
        else if (anchor.X >= workArea.Right)
        {
            left = workArea.Right - popupSize.Width;
            top = anchor.Y - popupSize.Height / 2d;
        }
        else
        {
            left = anchor.X - popupSize.Width / 2d;
            top = anchor.Y - popupSize.Height;
        }

        left = Math.Clamp(left, workArea.Left, workArea.Right - popupSize.Width);
        top = Math.Clamp(top, workArea.Top, workArea.Bottom - popupSize.Height);
        return new Rect(left, top, popupSize.Width, popupSize.Height);
    }

    public static Rect ConstrainSaved(
        Rect saved,
        IReadOnlyList<Rect> workAreas,
        Rect fallbackWorkArea,
        WpfSize minimumSize,
        double visibleDragHeight)
    {
        ArgumentNullException.ThrowIfNull(workAreas);

        if (!IsValidRect(saved))
        {
            throw new ArgumentOutOfRangeException(nameof(saved));
        }

        if (workAreas.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workAreas));
        }

        foreach (var workArea in workAreas)
        {
            if (!IsValidRect(workArea))
            {
                throw new ArgumentOutOfRangeException(nameof(workAreas));
            }
        }

        if (!IsValidRect(fallbackWorkArea))
        {
            throw new ArgumentOutOfRangeException(nameof(fallbackWorkArea));
        }

        if (!IsPositiveFinite(minimumSize.Width) || !IsPositiveFinite(minimumSize.Height))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSize));
        }

        if (!IsPositiveFinite(visibleDragHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(visibleDragHeight));
        }

        var selectedWorkArea = fallbackWorkArea;
        var largestIntersectionArea = 0d;
        foreach (var workArea in workAreas)
        {
            var intersectionWidth = Math.Max(
                0d,
                Math.Min(saved.Right, workArea.Right) - Math.Max(saved.Left, workArea.Left));
            var intersectionHeight = Math.Max(
                0d,
                Math.Min(saved.Bottom, workArea.Bottom) - Math.Max(saved.Top, workArea.Top));
            var intersectionArea = intersectionWidth * intersectionHeight;
            if (intersectionArea > largestIntersectionArea)
            {
                largestIntersectionArea = intersectionArea;
                selectedWorkArea = workArea;
            }
        }

        var width = Math.Min(
            selectedWorkArea.Width,
            Math.Max(saved.Width, minimumSize.Width));
        var height = Math.Min(
            selectedWorkArea.Height,
            Math.Max(saved.Height, minimumSize.Height));

        var left = Math.Clamp(
            saved.Left,
            selectedWorkArea.Left,
            selectedWorkArea.Right - width);
        var top = height <= selectedWorkArea.Height
            ? Math.Clamp(
                saved.Top,
                selectedWorkArea.Top,
                selectedWorkArea.Bottom - height)
            : Math.Clamp(
                saved.Top,
                selectedWorkArea.Top - height + Math.Min(visibleDragHeight, height),
                selectedWorkArea.Bottom - Math.Min(visibleDragHeight, height));

        return new Rect(left, top, width, height);
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;

    private static bool IsValidRect(Rect rect) =>
        !rect.IsEmpty
        && double.IsFinite(rect.X)
        && double.IsFinite(rect.Y)
        && IsPositiveFinite(rect.Width)
        && IsPositiveFinite(rect.Height)
        && double.IsFinite(rect.Right)
        && double.IsFinite(rect.Bottom);
}
