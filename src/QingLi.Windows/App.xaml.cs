using System.IO;
using System.Diagnostics;
using System.Windows;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.Holidays;
using QingLi.Core.Reminders;
using QingLi.Core.Settings;
using QingLi.Infrastructure.Birthdays;
using QingLi.Infrastructure.Data;
using QingLi.Infrastructure.Holidays;
using QingLi.Infrastructure.Reminders;
using QingLi.Windows.Notifications;
using QingLi.Windows.Scheduling;
using QingLi.Infrastructure.Settings;
using QingLi.Windows.Shell;
using QingLi.Windows.Startup;
using QingLi.Windows.Tray;
using QingLi.Windows.ViewModels;
using QingLi.Windows.Views;

namespace QingLi.Windows;

public partial class App : System.Windows.Application
{
    private CalendarPopupViewModel? _calendarPopupViewModel;
    private CalendarPopupWindow? _calendarPopupWindow;
    private TrayIconService? _trayIconService;
    private ISettingsStore? _settingsStore;
    private AppSettings _appSettings = AppSettings.Default;
    private SingletonWindowHost? _birthdayManagerHost;
    private SingletonWindowHost? _settingsHost;
    private IReminderSuppression? _reminderSuppression;
    private IReminderHistoryRepository? _reminderHistory;
    private ReminderScheduler? _reminderScheduler;
    private WindowsNotificationService? _notificationService;
    private SingleInstanceCoordinator? _singleInstanceCoordinator;

    public IBirthdayRepository BirthdayRepository { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            _singleInstanceCoordinator = new SingleInstanceCoordinator("QingLi");
            if (!await _singleInstanceCoordinator.TryAcquireAsync())
            {
                await _singleInstanceCoordinator.SignalPrimaryAsync("show-calendar");
                Shutdown();
                return;
            }

            _singleInstanceCoordinator.ActivationRequested += HandleActivationRequested;
            _calendarPopupViewModel = await CreateCalendarPopupViewModelAsync();
            _birthdayManagerHost = new SingletonWindowHost(() => new WindowAdapter(CreateBirthdayManagerWindow()));
            _settingsHost = new SingletonWindowHost(() => new WindowAdapter(CreateSettingsWindow()));
            _trayIconService = new TrayIconService(
                ToggleCalendar,
                onAddBirthday: ShowBirthdayManager,
                onOpenSettings: ShowSettings,
                onPauseTodayReminders: PauseTodayReminders,
                onExit: Shutdown);
            CreateReminderScheduler();

            if (_reminderScheduler is not null)
            {
                _reminderScheduler.CheckFailed += HandleReminderCheckFailed;

                if (_notificationService is { IsAvailable: false })
                {
                    _notificationService.ShowUnavailableWarning();
                }
                else
                {
                    try
                    {
                        await _reminderScheduler.CheckAsync(DateTimeOffset.Now, CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        HandleReminderCheckFailed(exception);
                    }

                    _reminderScheduler.Start();
                }
            }

            ShowCalendar();
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
        if (_singleInstanceCoordinator is not null)
        {
            _singleInstanceCoordinator.ActivationRequested -= HandleActivationRequested;
            _singleInstanceCoordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _singleInstanceCoordinator = null;
        }

        if (_reminderScheduler is not null)
        {
            _reminderScheduler.CheckFailed -= HandleReminderCheckFailed;
            _reminderScheduler.Dispose();
        }

        _notificationService?.Dispose();
        _trayIconService?.Dispose();
        _calendarPopupWindow?.Close();
        base.OnExit(e);
    }

    private async Task<CalendarPopupViewModel> CreateCalendarPopupViewModelAsync()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        _settingsStore = new JsonSettingsStore(Path.Combine(AppPaths.DataDirectory, "settings.json"));
        _appSettings = await _settingsStore.LoadAsync(CancellationToken.None);

        var connectionFactory = new SqliteConnectionFactory(AppPaths.DatabasePath);
        var migrationResult = await new DatabaseMigrator(connectionFactory).TryMigrateAsync(CancellationToken.None);

        if (!migrationResult.IsWritable)
        {
            throw new InvalidOperationException(
                migrationResult.ErrorMessage ?? "Unable to initialize the QingLi database.");
        }

        BirthdayRepository = new SqliteBirthdayRepository(connectionFactory);
        _reminderSuppression = new SqliteReminderSuppression(connectionFactory);
        _reminderHistory = new SqliteReminderHistoryRepository(connectionFactory);

        var holidayDefinitions = await LoadHolidayDefinitionsAsync();
        var calendarMonthService = new CalendarMonthService(
            new LunarCalendarService(),
            new SolarTermService(),
            new HolidayService(holidayDefinitions));

        var viewModel = new CalendarPopupViewModel(
            calendarMonthService,
            BirthdayRepository,
            new BirthdayOccurrenceService(),
            DateOnly.FromDateTime(DateTime.Today),
            _appSettings.FirstDayOfWeek);

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

        ShowCalendar();
    }

    private void ShowCalendar()
    {
        if (_calendarPopupViewModel is null)
        {
            return;
        }

        if (_calendarPopupWindow is { IsVisible: true })
        {
            _calendarPopupWindow.Activate();
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

    private void HandleActivationRequested(string command)
    {
        if (string.Equals(command, "show-calendar", StringComparison.Ordinal))
        {
            Dispatcher.BeginInvoke(ShowCalendar);
        }
    }

    private void ShowBirthdayManager() => _birthdayManagerHost?.Show();

    private void ShowSettings() => _settingsHost?.Show();

    private void CreateReminderScheduler()
    {
        if (_trayIconService is null || _reminderSuppression is null || _reminderHistory is null)
        {
            return;
        }

        _notificationService = new WindowsNotificationService(_trayIconService, _ => ShowBirthdayManager());
        var startupTime = DateTimeOffset.Now;
        _reminderScheduler = new ReminderScheduler(
            BirthdayRepository,
            new ReminderPlanner(new BirthdayOccurrenceService()),
            _reminderHistory,
            _reminderSuppression,
            _notificationService,
            new DateTimeOffset(startupTime.Date, startupTime.Offset));
    }

    private async void PauseTodayReminders()
    {
        if (_reminderSuppression is null)
        {
            return;
        }

        try
        {
            await _reminderSuppression.SuppressAsync(
                DateOnly.FromDateTime(DateTime.Today),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"无法暂停今日提醒：{exception.Message}",
                "轻历",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void HandleReminderCheckFailed(Exception exception)
    {
        if (exception is NotificationUnavailableException)
        {
            _reminderScheduler?.Dispose();
            _reminderScheduler = null;
            return;
        }

        Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(
                $"生日提醒检查失败：{exception.Message}",
                "轻历",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        });
    }

    private BirthdayManagerWindow CreateBirthdayManagerWindow()
    {
        var viewModel = new BirthdayManagerViewModel(
            BirthdayRepository,
            new BirthdayOccurrenceService(),
            () => DateOnly.FromDateTime(DateTime.Today));

        return new BirthdayManagerWindow(viewModel);
    }

    private SettingsWindow CreateSettingsWindow()
    {
        if (_settingsStore is null)
        {
            throw new InvalidOperationException("Settings store is not initialized.");
        }

        var startupTaskService = new StartupTaskService();
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to resolve the current process path.");

        var viewModel = new SettingsViewModel(
            _settingsStore,
            startupTaskService,
            executablePath,
            () => SystemParameters.HighContrast,
            OpenDirectory);

        return new SettingsWindow(viewModel);
    }

    private static void OpenDirectory(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
