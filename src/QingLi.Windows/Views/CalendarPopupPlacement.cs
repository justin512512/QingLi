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
            if (intersectionArea > largestIntersectionArea
                || (intersectionArea > 0d
                    && intersectionArea == largestIntersectionArea
                    && IsPreferredForEqualIntersection(
                        workArea,
                        selectedWorkArea,
                        fallbackWorkArea)))
            {
                largestIntersectionArea = intersectionArea;
                selectedWorkArea = workArea;
            }
        }

        var width = Math.Min(
            Math.Max(saved.Width, minimumSize.Width),
            Math.Max(selectedWorkArea.Width, minimumSize.Width));
        var height = Math.Min(
            Math.Max(saved.Height, minimumSize.Height),
            Math.Max(selectedWorkArea.Height, minimumSize.Height));

        var left = width <= selectedWorkArea.Width
            ? Math.Clamp(
                saved.Left,
                selectedWorkArea.Left,
                selectedWorkArea.Right - width)
            : Math.Clamp(
                saved.Left,
                selectedWorkArea.Right - width,
                selectedWorkArea.Left);
        var top = height <= selectedWorkArea.Height
            ? Math.Clamp(
                saved.Top,
                selectedWorkArea.Top,
                selectedWorkArea.Bottom - height)
            : Math.Clamp(
                saved.Top,
                selectedWorkArea.Top,
                selectedWorkArea.Bottom
                    - Math.Min(visibleDragHeight, selectedWorkArea.Height));

        return new Rect(left, top, width, height);
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;

    private static bool IsPreferredForEqualIntersection(
        Rect candidate,
        Rect current,
        Rect fallback)
    {
        var candidateIsFallback = candidate == fallback;
        var currentIsFallback = current == fallback;
        if (candidateIsFallback != currentIsFallback)
        {
            return candidateIsFallback;
        }

        if (candidate.Left != current.Left)
        {
            return candidate.Left < current.Left;
        }

        if (candidate.Top != current.Top)
        {
            return candidate.Top < current.Top;
        }

        if (candidate.Width != current.Width)
        {
            return candidate.Width < current.Width;
        }

        return candidate.Height < current.Height;
    }

    private static bool IsValidRect(Rect rect) =>
        !rect.IsEmpty
        && double.IsFinite(rect.X)
        && double.IsFinite(rect.Y)
        && IsPositiveFinite(rect.Width)
        && IsPositiveFinite(rect.Height)
        && double.IsFinite(rect.Right)
        && double.IsFinite(rect.Bottom);
}
