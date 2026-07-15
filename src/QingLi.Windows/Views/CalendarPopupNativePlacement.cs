using System.Windows;
using QingLi.Windows.Interop;

namespace QingLi.Windows.Views;

internal static class CalendarPopupNativePlacement
{
    private const uint SwpNoZOrder = 0x0004;

    internal static void Apply(
        nint windowHandle,
        Rect physicalBounds,
        Func<nint, int, int, int, int, bool>? setWindowPosition = null)
    {
        if (windowHandle == nint.Zero
            || physicalBounds.IsEmpty
            || !double.IsFinite(physicalBounds.Left)
            || !double.IsFinite(physicalBounds.Top)
            || !double.IsFinite(physicalBounds.Width)
            || !double.IsFinite(physicalBounds.Height)
            || physicalBounds.Width <= 0
            || physicalBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalBounds));
        }

        var x = checked((int)Math.Round(physicalBounds.Left));
        var y = checked((int)Math.Round(physicalBounds.Top));
        var width = checked((int)Math.Round(physicalBounds.Width));
        var height = checked((int)Math.Round(physicalBounds.Height));
        setWindowPosition ??= static (handle, left, top, targetWidth, targetHeight) =>
            User32.SetWindowPos(
                handle,
                nint.Zero,
                left,
                top,
                targetWidth,
                targetHeight,
                SwpNoZOrder | User32.SwpNoActivate | User32.SwpNoOwnerZOrder);

        if (!setWindowPosition(windowHandle, x, y, width, height))
        {
            throw new InvalidOperationException("Unable to apply the calendar popup's physical bounds.");
        }
    }
}
