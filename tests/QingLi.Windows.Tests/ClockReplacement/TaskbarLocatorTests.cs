using System.Windows;
using QingLi.Windows.ClockReplacement;

namespace QingLi.Windows.Tests.ClockReplacement;

public sealed class TaskbarLocatorTests
{
    private static readonly Rect PrimaryMonitor = new(0, 0, 1920, 1080);
    private static readonly Rect BottomTaskbar = new(0, 1040, 1920, 40);
    private static readonly Rect SecondaryMonitor = new(1920, 0, 2560, 1440);
    private static readonly Rect SecondaryTaskbar = new(1920, 1392, 2560, 48);

    [Fact]
    public void Bottom_taskbar_places_clock_inside_right_edge()
    {
        var geometry = new TaskbarGeometry(BottomTaskbar, TaskbarEdge.Bottom, 1.0);

        var bounds = ClockWindowPlacement.Calculate(geometry, new Size(104, 40));

        Assert.Equal(new Rect(1816, 1040, 104, 40), bounds);
    }

    [Fact]
    public void Placement_uses_physical_pixels_without_scaling_size_again()
    {
        var geometry = new TaskbarGeometry(BottomTaskbar, TaskbarEdge.Bottom, 1.5);

        var bounds = ClockWindowPlacement.Calculate(geometry, new Size(104, 40));

        Assert.Equal(new Rect(1816, 1040, 104, 40), bounds);
    }

    [Theory]
    [InlineData(TaskbarEdge.Left)]
    [InlineData(TaskbarEdge.Top)]
    [InlineData(TaskbarEdge.Right)]
    public void Placement_rejects_non_bottom_taskbars(TaskbarEdge edge)
    {
        var geometry = new TaskbarGeometry(BottomTaskbar, edge, 1.0);

        Assert.Throws<NotSupportedException>(() =>
            ClockWindowPlacement.Calculate(geometry, new Size(104, 40)));
    }

    [Fact]
    public void GetPrimary_returns_valid_bottom_taskbar_with_dpi()
    {
        var native = new FakeTaskbarNativeApi
        {
            Result = new TaskbarNativeData(BottomTaskbar, TaskbarEdge.Bottom, PrimaryMonitor, 144)
        };

        var geometry = new TaskbarLocator(native).GetPrimary();

        Assert.NotNull(geometry);
        Assert.Equal(BottomTaskbar, geometry.Bounds);
        Assert.Equal(TaskbarEdge.Bottom, geometry.Edge);
        Assert.Equal(1.5, geometry.DpiScale);
        Assert.Equal(1, native.QueryCount);
    }

    [Fact]
    public void GetPrimary_rejects_unsupported_edge()
    {
        var native = new FakeTaskbarNativeApi
        {
            Result = new TaskbarNativeData(BottomTaskbar, TaskbarEdge.Top, PrimaryMonitor, 96)
        };

        Assert.Null(new TaskbarLocator(native).GetPrimary());
    }

    [Theory]
    [MemberData(nameof(InvalidTaskbars))]
    public void GetPrimary_rejects_invalid_or_implausible_geometry(TaskbarNativeData data)
    {
        var native = new FakeTaskbarNativeApi { Result = data };

        Assert.Null(new TaskbarLocator(native).GetPrimary());
    }

    [Fact]
    public void GetPrimary_returns_null_when_shell_query_fails()
    {
        Assert.Null(new TaskbarLocator(new FakeTaskbarNativeApi()).GetPrimary());
    }

    [Fact]
    public void GetForPoint_returns_primary_taskbar_for_point_on_primary_monitor()
    {
        var native = new FakeTaskbarNativeApi
        {
            PointResult = new TaskbarNativeData(BottomTaskbar, TaskbarEdge.Bottom, PrimaryMonitor, 96)
        };

        var geometry = new TaskbarLocator(native).GetForPoint(new Point(100, 100));

        Assert.NotNull(geometry);
        Assert.Equal(BottomTaskbar, geometry.Bounds);
        Assert.Equal(1, native.PointQueryCount);
    }

    [Fact]
    public void GetForPoint_returns_taskbar_on_secondary_monitor()
    {
        var native = new FakeTaskbarNativeApi
        {
            PointResult = new TaskbarNativeData(
                SecondaryTaskbar,
                TaskbarEdge.Bottom,
                SecondaryMonitor,
                144)
        };

        var geometry = new TaskbarLocator(native).GetForPoint(new Point(2500, 100));

        Assert.NotNull(geometry);
        Assert.Equal(SecondaryTaskbar, geometry.Bounds);
        Assert.Equal(1.5, geometry.DpiScale);
    }

    [Fact]
    public void GetForPoint_returns_null_when_no_taskbar_matches_point()
    {
        var native = new FakeTaskbarNativeApi();

        Assert.Null(new TaskbarLocator(native).GetForPoint(new Point(2500, 100)));
        Assert.Equal(1, native.PointQueryCount);
    }

    public static TheoryData<TaskbarNativeData> InvalidTaskbars => new()
    {
        new(new Rect(0, 1040, 0, 40), TaskbarEdge.Bottom, PrimaryMonitor, 96),
        new(new Rect(0, 0, 1920, 40), TaskbarEdge.Bottom, PrimaryMonitor, 96),
        new(new Rect(0, 700, 1920, 380), TaskbarEdge.Bottom, PrimaryMonitor, 96),
        new(new Rect(-100, 1040, 2020, 40), TaskbarEdge.Bottom, PrimaryMonitor, 96),
        new(BottomTaskbar, TaskbarEdge.Bottom, PrimaryMonitor, 0)
    };

    private sealed class FakeTaskbarNativeApi : ITaskbarNativeApi
    {
        public TaskbarNativeData? Result { get; init; }

        public TaskbarNativeData? PointResult { get; init; }

        public int QueryCount { get; private set; }

        public int PointQueryCount { get; private set; }

        public bool TryGetPrimaryTaskbar(out TaskbarNativeData data)
        {
            QueryCount++;
            data = Result ?? default;
            return Result.HasValue;
        }

        public bool TryGetTaskbarForPoint(Point screenPoint, out TaskbarNativeData data)
        {
            PointQueryCount++;
            data = PointResult ?? default;
            return PointResult.HasValue;
        }
    }
}
