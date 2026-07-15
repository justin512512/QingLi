using System.Windows;
using QingLi.Windows.ClockReplacement;

namespace QingLi.Windows.Tests.ClockReplacement;

public sealed class ClockWindowControllerTests
{
    private static readonly TaskbarGeometry Geometry = new(
        new Rect(0, 1040, 1920, 40),
        TaskbarEdge.Bottom,
        1.5);

    [Fact]
    public async Task Show_reapplies_position_after_Wpf_makes_window_visible()
    {
        var calls = new List<string>();
        var window = new FakeClockWindow(calls);
        var positioner = new FakePositioner(calls, succeeds: true);
        using var controller = new ClockWindowController(
            new FakeLocator(Geometry),
            () => window,
            positioner);

        var shown = await controller.ShowAsync(default);

        Assert.True(shown);
        Assert.Equal(["handle", "position", "show", "position"], calls);
        Assert.Equal(new Rect(1764, 1040, 156, 40), positioner.Bounds);
    }

    [Fact]
    public async Task Failed_position_hides_window_and_reports_failure()
    {
        var calls = new List<string>();
        var window = new FakeClockWindow(calls);
        using var controller = new ClockWindowController(
            new FakeLocator(Geometry),
            () => window,
            new FakePositioner(calls, succeeds: false));

        var shown = await controller.ShowAsync(default);

        Assert.False(shown);
        Assert.Equal(["handle", "position", "hide"], calls);
    }

    [Fact]
    public async Task Unsupported_taskbar_does_not_create_window()
    {
        var created = false;
        using var controller = new ClockWindowController(
            new FakeLocator(null),
            () =>
            {
                created = true;
                return new FakeClockWindow([]);
            },
            new FakePositioner([], succeeds: true));

        var shown = await controller.ShowAsync(default);

        Assert.False(shown);
        Assert.False(created);
    }

    [Fact]
    public async Task Point_show_uses_matching_monitor_lookup()
    {
        var locator = new FakeLocator(Geometry);
        using var controller = new ClockWindowController(
            locator,
            () => new FakeClockWindow([]),
            new FakePositioner([], succeeds: true));
        var point = new Point(2500, 100);

        Assert.True(await controller.ShowAsync(point, default));
        Assert.Equal(point, locator.LastPoint);
    }

    private sealed class FakeLocator(TaskbarGeometry? geometry) : ITaskbarGeometryLocator
    {
        public Point? LastPoint { get; private set; }

        public TaskbarGeometry? GetPrimary() => geometry;

        public TaskbarGeometry? GetForPoint(Point screenPoint)
        {
            LastPoint = screenPoint;
            return geometry;
        }
    }

    private sealed class FakeClockWindow(List<string> calls) : ITaskbarClockWindow
    {
        public nint EnsureHandle()
        {
            calls.Add("handle");
            return 42;
        }

        public void ShowClock() => calls.Add("show");

        public void HideClock() => calls.Add("hide");

        public void Dispose()
        {
        }
    }

    private sealed class FakePositioner(List<string> calls, bool succeeds) : ITaskbarWindowPositioner
    {
        public Rect? Bounds { get; private set; }

        public bool TryPosition(nint windowHandle, Rect physicalBounds)
        {
            calls.Add("position");
            Bounds = physicalBounds;
            return succeeds;
        }
    }
}
