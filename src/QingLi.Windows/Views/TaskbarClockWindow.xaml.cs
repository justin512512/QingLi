using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.InteropServices;
using QingLi.Core.Settings;
using QingLi.Windows.ClockReplacement;
using QingLi.Windows.Interop;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Views;

public partial class TaskbarClockWindow : Window, ITaskbarClockWindow
{
    private readonly TaskbarClockViewModel _viewModel;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly Action _onPrimaryClick;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public TaskbarClockWindow(
        TaskbarClockViewModel viewModel,
        Func<AppSettings> settingsProvider,
        Action onPrimaryClick)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _onPrimaryClick = onPrimaryClick ?? throw new ArgumentNullException(nameof(onPrimaryClick));

        InitializeComponent();
        DataContext = _viewModel;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        Refresh();
    }

    public nint EnsureHandle()
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        ApplyNoActivateStyle(handle);
        return handle;
    }

    public void ShowClock()
    {
        Refresh();
        _timer.Start();
        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideClock()
    {
        _timer.Stop();
        Hide();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        Close();
    }

    private static void ApplyNoActivateStyle(nint handle)
    {
        Marshal.SetLastPInvokeError(0);
        var style = User32.GetWindowLongPtr(handle, User32.GwlExStyle);
        var getError = Marshal.GetLastPInvokeError();
        if (style == 0 && getError != 0)
        {
            throw new Win32Exception(getError);
        }

        Marshal.SetLastPInvokeError(0);
        var previousStyle = User32.SetWindowLongPtr(
            handle,
            User32.GwlExStyle,
            style | User32.WsExNoActivate | User32.WsExToolWindow);
        var setError = Marshal.GetLastPInvokeError();
        if (previousStyle == 0 && setError != 0)
        {
            throw new Win32Exception(setError);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e) => Refresh();

    private void Refresh() => _viewModel.Update(DateTimeOffset.Now, _settingsProvider());

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _onPrimaryClick();
            e.Handled = true;
        }
    }
}
