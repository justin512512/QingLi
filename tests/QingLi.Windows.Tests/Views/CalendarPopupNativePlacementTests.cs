using System.Windows;
using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupNativePlacementTests
{
    [Fact]
    public void ApplyPassesRoundedPhysicalBoundsIncludingNegativeOrigins()
    {
        (nint Handle, int X, int Y, int Width, int Height)? actual = null;

        CalendarPopupNativePlacement.Apply(
            (nint)42,
            new Rect(-2400.4, -100.6, 1560.2, 780.4),
            (handle, x, y, width, height) =>
            {
                actual = (handle, x, y, width, height);
                return true;
            });

        Assert.Equal(((nint)42, -2400, -101, 1560, 780), actual);
    }

    [Fact]
    public void ApplyReportsNativePlacementFailure()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CalendarPopupNativePlacement.Apply(
                (nint)42,
                new Rect(1920, 0, 1560, 780),
                (_, _, _, _, _) => false));
    }
}
