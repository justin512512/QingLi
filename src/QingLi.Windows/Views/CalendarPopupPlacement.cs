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

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;
}
