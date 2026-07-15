using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using QingLi.Windows.Interop;
using QingLi.Windows.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using FormsCursor = System.Windows.Forms.Cursor;
using FormsScreen = System.Windows.Forms.Screen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace QingLi.Windows.Views;

public partial class CalendarPopupWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const double DefaultPopupWidth = 1040;
    private const double DefaultPopupHeight = 520;
    private const double VisibleDragHeight = 28;
    private readonly CalendarPopupDeactivationGuard _deactivationGuard = new(TimeSpan.FromMilliseconds(750));
    private readonly CalendarPopupLayoutSession _layoutSession;
    private HwndSource? _hwndSource;
    private bool _applyingLayout;
    private bool _layoutInitialized;
    private Task? _layoutRestoreTask;

    public CalendarPopupWindow(
        CalendarDashboardViewModel viewModel,
        ICalendarPopupLayoutStore layoutStore)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(layoutStore);

        InitializeComponent();
        DataContext = viewModel;
        _layoutSession = new CalendarPopupLayoutSession(
            layoutStore,
            GetPhysicalScreens,
            persistenceContext: new DispatcherSynchronizationContext(Dispatcher));
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public event Action<DateOnly>? AddBirthdayRequested;
    public event Action<DateOnly>? AddAnniversaryRequested;
    public event Action? SettingsRequested;
    public event Action<UpcomingEventViewModel>? UpcomingEventRequested;

    private CalendarDashboardViewModel? ViewModel => DataContext as CalendarDashboardViewModel;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(OnWindowMessage);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(OnWindowMessage);
        _hwndSource = null;
        IsVisibleChanged -= OnIsVisibleChanged;
        _layoutInitialized = false;
        _layoutSession.Dispose();
        base.OnClosed(e);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        RecordCurrentLayout();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        RecordCurrentLayout();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && IsLoaded && !_layoutInitialized)
        {
            _ = EnsureLayoutRestoredAsync();
        }
    }

    private nint OnWindowMessage(
        nint windowHandle,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message != WmNcHitTest || !User32.GetWindowRect(windowHandle, out var bounds))
        {
            return nint.Zero;
        }

        var packedPoint = lParam.ToInt64();
        var screenX = unchecked((short)(packedPoint & 0xffff));
        var screenY = unchecked((short)((packedPoint >> 16) & 0xffff));
        var dpi = User32.GetDpiForWindow(windowHandle);
        var dpiScale = dpi == 0 ? 1d : dpi / 96d;
        var target = WindowResizeHitTest.Classify(
            screenX - bounds.Left,
            screenY - bounds.Top,
            bounds.Right - bounds.Left,
            bounds.Bottom - bounds.Top,
            dpiScale,
            dpiScale);

        if (target == WindowResizeHitTarget.Client)
        {
            return nint.Zero;
        }

        handled = true;
        return (nint)(int)target;
    }

    private async void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (ViewModel is null) return;
        var offset = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Up => -7,
            Key.Down => 7,
            _ => 0
        };
        if (offset != 0)
        {
            await ViewModel.SelectDateAsync(ViewModel.SelectedDate.AddDays(offset));
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp)
        {
            await ViewModel.PreviousMonthCommand.ExecuteAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown)
        {
            await ViewModel.NextMonthCommand.ExecuteAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Home)
        {
            await ViewModel.TodayCommand.ExecuteAsync();
            e.Handled = true;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_layoutInitialized)
            {
                await EnsureLayoutRestoredAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Reset owns the next physical placement.
        }

        Activate();
        Focus();
        _deactivationGuard.MarkShown();
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        if (_deactivationGuard.ShouldClose()) Hide();
    }

    private void OnDragHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse button can be released before WPF enters its native drag loop.
        }

        e.Handled = true;
    }

    private async void OnCalendarSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && CalendarDaysList.SelectedItem is CalendarDayViewModel day)
        {
            await ViewModel.SelectDateAsync(day.Date);
        }
    }

    private void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string url } && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private void OnUpcomingClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: UpcomingEventViewModel item }) UpcomingEventRequested?.Invoke(item);
    }

    private void OnAddBirthdayClick(object sender, RoutedEventArgs e) => AddBirthdayRequested?.Invoke(ViewModel?.SelectedDate ?? DateOnly.FromDateTime(DateTime.Today));
    private void OnAddAnniversaryClick(object sender, RoutedEventArgs e) => AddAnniversaryRequested?.Invoke(ViewModel?.SelectedDate ?? DateOnly.FromDateTime(DateTime.Today));
    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    public async Task ResetLayoutAsync()
    {
        var activeRestore = _layoutRestoreTask;
        await _layoutSession.ResetAsync();
        if (activeRestore is not null)
        {
            try
            {
                await activeRestore;
            }
            catch (OperationCanceledException)
            {
            }

            if (ReferenceEquals(_layoutRestoreTask, activeRestore))
            {
                _layoutRestoreTask = null;
            }
        }

        _layoutInitialized = false;

        _applyingLayout = true;
        try
        {
            Width = DefaultPopupWidth;
            Height = DefaultPopupHeight;
        }
        finally
        {
            _applyingLayout = false;
        }

        if (IsVisible)
        {
            await EnsureLayoutRestoredAsync();
        }
    }

    private Task EnsureLayoutRestoredAsync()
    {
        if (_layoutInitialized)
        {
            return Task.CompletedTask;
        }

        if (_layoutRestoreTask is not null)
        {
            return _layoutRestoreTask;
        }

        var restoreTask = RestoreLayoutAsync();
        if (restoreTask.IsCompleted)
        {
            return restoreTask;
        }

        _layoutRestoreTask = restoreTask;
        _ = ClearRestoreTaskWhenCompleteAsync(restoreTask);
        return restoreTask;
    }

    private async Task ClearRestoreTaskWhenCompleteAsync(Task restoreTask)
    {
        try
        {
            await restoreTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_layoutRestoreTask, restoreTask))
            {
                _layoutRestoreTask = null;
            }
        }
    }

    private async Task RestoreLayoutAsync()
    {
        var defaultBounds = PositionNearClickedTaskbar();
        Rect bounds;
        try
        {
            bounds = await _layoutSession.InitializeAsync(
                defaultBounds,
                new WpfSize(MinWidth, MinHeight),
                VisibleDragHeight);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            bounds = defaultBounds;
        }

        _applyingLayout = true;
        try
        {
            CalendarPopupNativePlacement.Apply(
                new WindowInteropHelper(this).Handle,
                bounds);
            _layoutInitialized = true;
        }
        finally
        {
            _applyingLayout = false;
        }
    }

    private void RecordCurrentLayout()
    {
        if (_applyingLayout || !_layoutInitialized)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != nint.Zero && User32.GetWindowRect(handle, out var physicalBounds))
        {
            _layoutSession.RecordLayoutChange(physicalBounds.ToRect());
        }
    }

    private Rect PositionNearClickedTaskbar()
    {
        try
        {
            var cursor = FormsCursor.Position;
            return CalendarPopupScreenGeometry.PlaceNearCursor(
                GetPhysicalScreens(),
                new WpfPoint(cursor.X, cursor.Y),
                new WpfSize(DefaultPopupWidth, DefaultPopupHeight));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            var cursor = FormsCursor.Position;
            var screen = FormsScreen.FromPoint(cursor);
            var workArea = screen.WorkingArea;
            var dpi = User32.GetDpiForSystem();
            var scale = dpi > 0 ? dpi / 96d : 1d;
            var width = DefaultPopupWidth * scale;
            var height = DefaultPopupHeight * scale;
            return new Rect(
                Math.Max(workArea.Left, workArea.Right - width - 12 * scale),
                Math.Max(workArea.Top, workArea.Bottom - height - 12 * scale),
                width,
                height);
        }
    }

    private static IReadOnlyList<CalendarPopupPhysicalScreen> GetPhysicalScreens() =>
        FormsScreen.AllScreens.Select(CalendarPopupMonitorDpi.GetForScreen).ToArray();
}
