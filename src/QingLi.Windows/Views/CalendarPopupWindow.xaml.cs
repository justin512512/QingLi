using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QingLi.Windows.ViewModels;
using WpfButton = System.Windows.Controls.Button;

namespace QingLi.Windows.Views;

public partial class CalendarPopupWindow : Window
{
    private readonly CalendarPopupDeactivationGuard _deactivationGuard = new(TimeSpan.FromMilliseconds(750));

    public CalendarPopupWindow(CalendarDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public CalendarPopupWindow(CalendarPopupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event Action<DateOnly>? AddBirthdayRequested;
    public event Action<DateOnly>? AddAnniversaryRequested;
    public event Action? SettingsRequested;
    public event Action<UpcomingEventViewModel>? UpcomingEventRequested;

    private CalendarDashboardViewModel? ViewModel => DataContext as CalendarDashboardViewModel;

    private async void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
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
        PositionInWorkArea();
        Activate();
        Focus();
        _deactivationGuard.MarkShown();
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        if (_deactivationGuard.ShouldClose()) Close();
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

    private void PositionInWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 12);
        Top = Math.Max(workArea.Top, workArea.Bottom - Height - 12);
    }
}
