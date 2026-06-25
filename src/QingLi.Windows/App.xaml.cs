using System.IO;
using System.Windows;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.Holidays;
using QingLi.Infrastructure.Birthdays;
using QingLi.Infrastructure.Data;
using QingLi.Infrastructure.Holidays;
using QingLi.Windows.Tray;
using QingLi.Windows.ViewModels;
using QingLi.Windows.Views;

namespace QingLi.Windows;

public partial class App : System.Windows.Application
{
    private CalendarPopupViewModel? _calendarPopupViewModel;
    private CalendarPopupWindow? _calendarPopupWindow;
    private TrayIconService? _trayIconService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            _calendarPopupViewModel = await CreateCalendarPopupViewModelAsync();
            _trayIconService = new TrayIconService(
                ToggleCalendar,
                onAddBirthday: () => { },
                onOpenSettings: () => { },
                onPauseTodayReminders: () => { },
                onExit: Shutdown);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"轻历无法启动：{exception.Message}",
                "轻历",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        _calendarPopupWindow?.Close();
        base.OnExit(e);
    }

    private async Task<CalendarPopupViewModel> CreateCalendarPopupViewModelAsync()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var databasePath = AppPaths.GetDatabasePath(localApplicationData);
        var databaseDirectory = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("Unable to resolve the QingLi data directory.");

        Directory.CreateDirectory(databaseDirectory);
        var connectionFactory = new SqliteConnectionFactory(databasePath);
        var migrationResult = await new DatabaseMigrator(connectionFactory).TryMigrateAsync(CancellationToken.None);

        if (!migrationResult.IsWritable)
        {
            throw new InvalidOperationException(
                migrationResult.ErrorMessage ?? "Unable to initialize the QingLi database.");
        }

        var holidayDefinitions = await LoadHolidayDefinitionsAsync();
        var calendarMonthService = new CalendarMonthService(
            new LunarCalendarService(),
            new SolarTermService(),
            new HolidayService(holidayDefinitions));

        var viewModel = new CalendarPopupViewModel(
            calendarMonthService,
            new SqliteBirthdayRepository(connectionFactory),
            new BirthdayOccurrenceService(),
            DateOnly.FromDateTime(DateTime.Today),
            DayOfWeek.Monday);

        await viewModel.InitializeAsync();
        return viewModel;
    }

    private static async Task<IReadOnlyList<HolidayDefinition>> LoadHolidayDefinitionsAsync()
    {
        try
        {
            var holidayPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Holidays", "cn-2026.json");
            if (!File.Exists(holidayPath))
            {
                return [];
            }

            var package = await new JsonHolidayProvider().ReadAsync(holidayPath);
            return package.Days;
        }
        catch
        {
            return [];
        }
    }

    private void ToggleCalendar()
    {
        if (_calendarPopupViewModel is null)
        {
            return;
        }

        if (_calendarPopupWindow is { IsVisible: true })
        {
            _calendarPopupWindow.Close();
            return;
        }

        if (_calendarPopupWindow is null)
        {
            _calendarPopupWindow = new CalendarPopupWindow(_calendarPopupViewModel);
            _calendarPopupWindow.Closed += (_, _) => _calendarPopupWindow = null;
        }

        _calendarPopupWindow.Show();
        _calendarPopupWindow.Activate();
    }
}
