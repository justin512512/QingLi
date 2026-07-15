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

    [Fact]
    public void SavedLayoutOnPrimaryMonitorUsesPrimaryWorkArea()
    {
        var saved = new Rect(100, 80, 900, 500);
        Rect[] workAreas = [new(0, 0, 1920, 1032), new(1920, 0, 1920, 1032)];

        var actual = CalendarPopupPlacement.ConstrainSaved(
            saved, workAreas, workAreas[0], new Size(760, 420), 32);

        Assert.Equal(saved, actual);
    }

    [Fact]
    public void SavedLayoutOnSecondaryMonitorUsesSecondaryWorkArea()
    {
        var saved = new Rect(2100, 80, 900, 500);
        Rect[] workAreas = [new(0, 0, 1920, 1032), new(1920, 0, 1920, 1032)];

        var actual = CalendarPopupPlacement.ConstrainSaved(
            saved, workAreas, workAreas[0], new Size(760, 420), 32);

        Assert.Equal(saved, actual);
    }

    [Fact]
    public void SavedLayoutChoosesWorkAreaWithLargestPositiveIntersection()
    {
        var saved = new Rect(1500, 100, 1000, 600);
        Rect[] workAreas = [new(0, 0, 1920, 1032), new(1920, 0, 1920, 1032)];

        var actual = CalendarPopupPlacement.ConstrainSaved(
            saved, workAreas, workAreas[0], new Size(760, 420), 32);

        Assert.Equal(new Rect(1920, 100, 1000, 600), actual);
    }

    [Fact]
    public void SavedLayoutOnRemovedMonitorMovesToFallbackWorkArea()
    {
        var saved = new Rect(4500, 100, 1000, 600);
        Rect[] workAreas = [new(0, 0, 1920, 1032), new(1920, 0, 1920, 1032)];

        var actual = CalendarPopupPlacement.ConstrainSaved(
            saved, workAreas, workAreas[0], new Size(760, 420), 32);

        Assert.Equal(new Rect(920, 100, 1000, 600), actual);
    }

    [Fact]
    public void PartiallyVisibleSavedLayoutIsMovedFullyInsideIntersectingWorkArea()
    {
        var saved = new Rect(-200, -100, 900, 500);

        var actual = CalendarPopupPlacement.ConstrainSaved(
            saved, [WorkArea], WorkArea, new Size(760, 420), 32);

        Assert.Equal(new Rect(0, 0, 900, 500), actual);
    }

    [Fact]
    public void OversizedSavedLayoutShrinksToSelectedWorkArea()
    {
        var workArea = new Rect(0, 0, 1280, 720);

        var actual = CalendarPopupPlacement.ConstrainSaved(
            new Rect(10, 10, 3000, 2000), [workArea], workArea,
            new Size(760, 420), 32);

        Assert.Equal(workArea, actual);
    }

    [Fact]
    public void UndersizedSavedLayoutExpandsToMinimumSizeAndRemainsVisible()
    {
        var workArea = new Rect(0, 0, 1280, 720);

        var actual = CalendarPopupPlacement.ConstrainSaved(
            new Rect(1000, 500, 300, 200), [workArea], workArea,
            new Size(760, 420), 32);

        Assert.Equal(new Rect(520, 300, 760, 420), actual);
    }

    [Fact]
    public void WorkAreaSmallerThanMinimumKeepsLayoutFiniteAndInsideWorkArea()
    {
        var workArea = new Rect(-100, -50, 600, 300);

        var actual = CalendarPopupPlacement.ConstrainSaved(
            new Rect(-1000, -1000, 200, 100), [workArea], workArea,
            new Size(760, 420), 32);

        Assert.Equal(workArea, actual);
        Assert.True(double.IsFinite(actual.X));
        Assert.True(double.IsFinite(actual.Y));
        Assert.True(actual.Width >= 0);
        Assert.True(actual.Height >= 0);
    }

    [Fact]
    public void SavedLayoutConstraintsRejectInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CalendarPopupPlacement.ConstrainSaved(
                WorkArea, null!, WorkArea, new Size(760, 420), 32));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupPlacement.ConstrainSaved(
                WorkArea, [], WorkArea, new Size(760, 420), 32));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupPlacement.ConstrainSaved(
                Rect.Empty, [WorkArea], WorkArea, new Size(760, 420), 32));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupPlacement.ConstrainSaved(
                WorkArea, [new Rect(0, 0, double.PositiveInfinity, 100)], WorkArea,
                new Size(760, 420), 32));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupPlacement.ConstrainSaved(
                WorkArea, [WorkArea], Rect.Empty, new Size(760, 420), 32));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupPlacement.ConstrainSaved(
                WorkArea, [WorkArea], WorkArea, new Size(0, 420), 32));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPopupPlacement.ConstrainSaved(
                WorkArea, [WorkArea], WorkArea, new Size(760, 420), double.NaN));
    }
}
