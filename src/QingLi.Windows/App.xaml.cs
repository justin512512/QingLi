using System.IO;
using System.Linq;
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

        _calendarPopupViewModel = await CreateCalendarPopupViewModelAsync();
        _trayIconService = new TrayIconService(
            ToggleCalendar,
            onAddBirthday: () => { },
            onOpenSettings: () => { },
            onPauseTodayReminders: () => { },
            onExit: Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        _calendarPopupWindow?.Close();
        base.OnExit(e);
    }

    private async Task<CalendarPopupViewModel> CreateCalendarPopupViewModelAsync()
    {
        var appDataDirectory = ResolveDataDirectory();

        var databasePath = Path.Combine(appDataDirectory, "qingli.db");
        var connectionFactory = new SqliteConnectionFactory(databasePath);
        await new DatabaseMigrator(connectionFactory).TryMigrateAsync(CancellationToken.None)
            .ConfigureAwait(false);

        var holidayDefinitions = await LoadHolidayDefinitionsAsync().ConfigureAwait(false);
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

        await viewModel.InitializeAsync().ConfigureAwait(false);
        return viewModel;
    }

    private static string ResolveDataDirectory()
    {
        var preferredPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QingLi");

        try
        {
            Directory.CreateDirectory(preferredPath);
            return preferredPath;
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        var fallbackPath = Path.Combine(AppContext.BaseDirectory, "QingLiData");
        Directory.CreateDirectory(fallbackPath);
        return fallbackPath;
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

            var package = await new JsonHolidayProvider().ReadAsync(holidayPath).ConfigureAwait(false);
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
