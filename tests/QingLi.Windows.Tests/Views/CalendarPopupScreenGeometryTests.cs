using System.Windows;
using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupScreenGeometryTests
{
    [Fact]
    public void WorkAreasUseEachMonitorsOwnDpi()
    {
        CalendarPopupPhysicalScreen[] screens =
        [
            new(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), 96, 96),
            new(new Rect(1920, 0, 2560, 1440), new Rect(1920, 0, 2560, 1400), 144, 144)
        ];

        var actual = CalendarPopupScreenGeometry.GetWorkAreasInDips(screens);

        Assert.Equal(new Rect(0, 0, 1920, 1040), actual[0]);
        Assert.Equal(new Rect(1280, 0, 2560d / 1.5d, 1400d / 1.5d), actual[1]);
    }

    [Fact]
    public void CursorUsesDpiOfContainingMonitor()
    {
        CalendarPopupPhysicalScreen[] screens =
        [
            new(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), 96, 96),
            new(new Rect(1920, 0, 2560, 1440), new Rect(1920, 0, 2560, 1400), 144, 144)
        ];

        var actual = CalendarPopupScreenGeometry.GetCursorGeometry(
            screens,
            new Point(3000, 1200));

        Assert.Equal(new Point(2000, 800), actual.Anchor);
        Assert.Equal(new Rect(1280, 0, 2560d / 1.5d, 1400d / 1.5d), actual.WorkArea);
    }

    [Fact]
    public void CursorFallbackSelectsNearestNegativeMonitorAndPreservesSignedCoordinates()
    {
        CalendarPopupPhysicalScreen[] screens =
        [
            new(new Rect(-2560, -200, 2560, 1440), new Rect(-2560, -160, 2560, 1400), 144, 144),
            new(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), 96, 96)
        ];

        var actual = CalendarPopupScreenGeometry.GetCursorGeometry(
            screens,
            new Point(-2700, -100));

        Assert.Equal(new Point(-1800, -100d / 1.5d), actual.Anchor);
        Assert.Equal(
            new Rect(-2560d / 1.5d, -160d / 1.5d, 2560d / 1.5d, 1400d / 1.5d),
            actual.WorkArea);
    }
}
