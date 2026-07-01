using System.Windows;
using QingLi.Windows.Interop;
using ScreenPoint = System.Windows.Point;
using PhysicalSize = System.Windows.Size;

namespace QingLi.Windows.ClockReplacement;

public interface ITaskbarClockWindow : IDisposable
{
    nint EnsureHandle();

    void ShowClock();

    void HideClock();
}

public interface ITaskbarWindowPositioner
{
    bool TryPosition(nint windowHandle, Rect physicalBounds);
}

public interface IClockWindowController
{
    Task<bool> ShowAsync(CancellationToken cancellationToken);

    void Hide();
}

public sealed class ClockWindowController : IClockWindowController, IDisposable
{
    private const double ClockWidthInDips = 104;

    private readonly ITaskbarGeometryLocator _locator;
    private readonly Func<ITaskbarClockWindow> _windowFactory;
    private readonly ITaskbarWindowPositioner _positioner;
    private ITaskbarClockWindow? _window;
    private bool _disposed;

    public ClockWindowController(
        ITaskbarGeometryLocator locator,
        Func<ITaskbarClockWindow> windowFactory,
        ITaskbarWindowPositioner positioner)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _positioner = positioner ?? throw new ArgumentNullException(nameof(positioner));
    }

    public Task<bool> ShowAsync(CancellationToken cancellationToken) =>
        ShowAsync(null, cancellationToken);

    public Task<bool> ShowAsync(
        ScreenPoint? screenPoint,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryPositionAndShow(screenPoint));
    }

    public Task<bool> RepositionAsync(
        ScreenPoint? screenPoint,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        Hide();
        return Task.FromResult(TryPositionAndShow(screenPoint));
    }

    public void Hide() => _window?.HideClock();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window?.Dispose();
        _window = null;
    }

    private bool TryPositionAndShow(ScreenPoint? screenPoint)
    {
        var geometry = screenPoint is { } point
            ? _locator.GetForPoint(point)
            : _locator.GetPrimary();
        if (geometry is null)
        {
            Hide();
            return false;
        }

        Rect bounds;
        try
        {
            bounds = ClockWindowPlacement.Calculate(
                geometry,
                new PhysicalSize(ClockWidthInDips * geometry.DpiScale, geometry.Bounds.Height));
        }
        catch (ArgumentException)
        {
            Hide();
            return false;
        }
        catch (NotSupportedException)
        {
            Hide();
            return false;
        }

        _window ??= _windowFactory();
        var handle = _window.EnsureHandle();
        if (handle == 0 || !_positioner.TryPosition(handle, bounds))
        {
            Hide();
            return false;
        }

        _window.ShowClock();
        return true;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}

public sealed class Win32TaskbarWindowPositioner : ITaskbarWindowPositioner
{
    public bool TryPosition(nint windowHandle, Rect physicalBounds)
    {
        if (windowHandle == 0 || !TryRound(physicalBounds.X, out var x) ||
            !TryRound(physicalBounds.Y, out var y) ||
            !TryRound(physicalBounds.Width, out var width) ||
            !TryRound(physicalBounds.Height, out var height) ||
            width <= 0 || height <= 0)
        {
            return false;
        }

        return User32.SetWindowPos(
            windowHandle,
            User32.HwndTopmost,
            x,
            y,
            width,
            height,
            User32.SwpNoActivate | User32.SwpNoOwnerZOrder);
    }

    private static bool TryRound(double value, out int rounded)
    {
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
        {
            rounded = default;
            return false;
        }

        rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return true;
    }
}
