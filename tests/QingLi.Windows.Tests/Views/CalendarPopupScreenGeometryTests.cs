using System.Windows;
using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupScreenGeometryTests
{
    private static readonly CalendarPopupPhysicalScreen Primary = new(
        @"\\.\DISPLAY1",
        new Rect(0, 0, 1920, 1080),
        new Rect(0, 0, 1920, 1040),
        96,
        96);

    private static readonly CalendarPopupPhysicalScreen ScaledRight = new(
        @"\\.\DISPLAY2",
        new Rect(1920, 0, 2560, 1440),
        new Rect(1920, 0, 2560, 1400),
        144,
        144);

    private static readonly CalendarPopupPhysicalScreen ScaledNegative = new(
        @"\\.\DISPLAY3",
        new Rect(-2560, -200, 2560, 1440),
        new Rect(-2560, -160, 2560, 1400),
        144,
        144);

    [Fact]
    public void DefaultPlacementUsesTargetMonitorPhysicalPixelsWithoutOverlap()
    {
        CalendarPopupPhysicalScreen[] screens = [Primary, ScaledRight];

        var primary = CalendarPopupScreenGeometry.PlaceNearCursor(
            screens,
            new Point(1800, 1080),
            new Size(1040, 520));
        var scaled = CalendarPopupScreenGeometry.PlaceNearCursor(
            screens,
            new Point(4300, 1440),
            new Size(1040, 520));

        Assert.Equal(new Rect(880, 520, 1040, 520), primary);
        Assert.Equal(new Rect(2920, 620, 1560, 780), scaled);
        Assert.True(primary.Right <= ScaledRight.Bounds.Left);
        Assert.True(scaled.Left >= ScaledRight.Bounds.Left);
    }

    [Fact]
    public void PersistedMonitorLocalLayoutRoundTripsToDistinctPhysicalBounds()
    {
        CalendarPopupPhysicalScreen[] screens = [Primary, ScaledRight, ScaledNegative];
        (CalendarPopupPhysicalScreen Target, Rect Bounds)[] cases =
        [
            (Primary, new Rect(120, 100, 1040, 520)),
            (ScaledRight, new Rect(2100, 150, 1560, 780)),
            (ScaledNegative, new Rect(-2400, -100, 1560, 780))
        ];

        foreach (var (target, physicalBounds) in cases)
        {
            var saved = CalendarPopupScreenGeometry.ToPersistedLayout(physicalBounds, screens);
            var restored = CalendarPopupScreenGeometry.RestoreSavedLayout(
                saved,
                screens,
                new Size(760, 420),
                28);

            Assert.Equal(target.DeviceName, saved.MonitorDeviceName);
            Assert.Equal(physicalBounds, restored);
        }
    }

    [Fact]
    public void PhysicalIntersectionSelectsNegativeMonitorDespiteMixedDpi()
    {
        CalendarPopupPhysicalScreen[] screens = [Primary, ScaledRight, ScaledNegative];
        var physicalBounds = new Rect(-2400, -100, 1560, 780);

        var saved = CalendarPopupScreenGeometry.ToPersistedLayout(physicalBounds, screens);

        Assert.Equal(ScaledNegative.DeviceName, saved.MonitorDeviceName);
        Assert.Equal(160d / 1.5d, saved.Left, 8);
        Assert.Equal(100d / 1.5d, saved.Top, 8);
        Assert.Equal(1040, saved.Width, 8);
        Assert.Equal(520, saved.Height, 8);
    }

    [Fact]
    public void MissingMonitorIdentityCannotBeMisinterpretedAsGlobalDips()
    {
        var legacy = new CalendarPopupLayout(1280, 0, 1040, 520, true);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupScreenGeometry.RestoreSavedLayout(
                legacy,
                [Primary, ScaledRight],
                new Size(760, 420),
                28));
    }

}
