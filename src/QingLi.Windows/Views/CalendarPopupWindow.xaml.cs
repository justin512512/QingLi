using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    private readonly CalendarPopupDeactivationGuard _deactivationGuard = new(TimeSpan.FromMilliseconds(750));
    private HwndSource? _hwndSource;

    public CalendarPopupWindow(CalendarDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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
        base.OnClosed(e);
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionNearClickedTaskbar();
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

    private void PositionNearClickedTaskbar()
    {
        try
        {
            var cursor = FormsCursor.Position;
            var screen = FormsScreen.FromPoint(cursor);
            var dpi = VisualTreeHelper.GetDpi(this);
            var workArea = new Rect(
                screen.WorkingArea.Left / dpi.DpiScaleX,
                screen.WorkingArea.Top / dpi.DpiScaleY,
                screen.WorkingArea.Width / dpi.DpiScaleX,
                screen.WorkingArea.Height / dpi.DpiScaleY);
            var anchor = new WpfPoint(cursor.X / dpi.DpiScaleX, cursor.Y / dpi.DpiScaleY);
            var placement = CalendarPopupPlacement.Calculate(workArea, new WpfSize(Width, Height), anchor);
            Left = placement.Left;
            Top = placement.Top;
        }
        catch (ArgumentOutOfRangeException)
        {
            var workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left, workArea.Right - Width - 12);
            Top = Math.Max(workArea.Top, workArea.Bottom - Height - 12);
        }
    }
}
