using System.Windows;
using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupPlacementTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1032);
    private static readonly Size Popup = new(900, 440);

    [Theory]
    [InlineData(1868, 1056, 1020, 592)]
    [InlineData(1868, -20, 1020, 0)]
    [InlineData(-20, 500, 0, 280)]
    [InlineData(1940, 500, 1020, 280)]
    public void PlacementStaysInWorkAreaNearClickedTaskbar(
        double anchorX,
        double anchorY,
        double expectedX,
        double expectedY)
    {
        var actual = CalendarPopupPlacement.Calculate(WorkArea, Popup, new Point(anchorX, anchorY));

        Assert.Equal(new Rect(expectedX, expectedY, 900, 440), actual);
    }

    [Fact]
    public void PlacementRejectsPopupLargerThanWorkArea()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupPlacement.Calculate(WorkArea, new Size(2000, 1200), new Point(1900, 1050)));
    }
}
