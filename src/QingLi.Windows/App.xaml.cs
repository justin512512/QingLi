using System.IO;
using System.Diagnostics;
using System.Windows;
using QingLi.Core.Almanac;
using QingLi.Core.Anniversaries;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.ClockReplacement;
using QingLi.Core.Holidays;
using QingLi.Core.History;
using QingLi.Core.Reminders;
using QingLi.Core.Settings;
using QingLi.Core.Upcoming;
using QingLi.Infrastructure.Anniversaries;
using QingLi.Infrastructure.Birthdays;
using QingLi.Infrastructure.ClockReplacement;
using QingLi.Infrastructure.Data;
using QingLi.Infrastructure.Holidays;
using QingLi.Infrastructure.History;
using QingLi.Infrastructure.Reminders;
using QingLi.Windows.Notifications;
using QingLi.Windows.ClockReplacement;
using QingLi.Windows.Scheduling;
using QingLi.Infrastructure.Settings;
using QingLi.Infrastructure.Updates;
using QingLi.Windows.Shell;
using QingLi.Windows.Startup;
using QingLi.Windows.Tray;
using QingLi.Windows.ViewModels;
using QingLi.Windows.Views;

namespace QingLi.Windows;

public partial class App : System.Windows.Application
{
    private CalendarDashboardViewModel? _calendarDashboardViewModel;
    private CalendarPopupWindow? _calendarPopupWindow;
    private ICalendarPopupLayoutStore? _calendarPopupLayoutStore;
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
    private ClockWindowController? _clockWindowController;
    private IClockReplacementCoordinator? _clockReplacementCoordinator;
    private ClockReplacementExitOrchestrator? _exitOrchestrator;
    private bool _isFirstRun;

    public IBirthdayRepository BirthdayRepository { get; private set; } = null!;
    public IAnniversaryRepository AnniversaryRepository { get; private set; } = null!;

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
            await InitializeSettingsAsync();
            _calendarPopupLayoutStore = new JsonCalendarPopupLayoutStore(
                Path.Combine(AppPaths.DataDirectory, "calendar-window-layout.json"));
            CreateClockReplacementServices();

            if (_clockReplacementCoordinator is not null)
            {
                var startup = new ClockReplacementStartupOrchestrator(
                    _clockReplacementCoordinator);
                var recovery = await startup.StartAsync(
                    _isFirstRun, _appSettings, CancellationToken.None);
                _appSettings = recovery.Settings;
                if (!recovery.Succeeded && !string.IsNullOrWhiteSpace(recovery.ErrorMessage))
                {
                    System.Windows.MessageBox.Show(
                        recovery.ErrorMessage,
                        "轻历",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            _trayIconService = new TrayIconService(
                ToggleCalendar,
                onAddBirthday: ShowBirthdayManager,
                onOpenSettings: ShowSettings,
                onPauseTodayReminders: PauseTodayReminders,
                onRestoreSystemClock: RestoreSystemClock,
                onExit: ExitSafely);
            _calendarDashboardViewModel = await CreateCalendarDashboardViewModelAsync();
            _birthdayManagerHost = new SingletonWindowHost(() => new WindowAdapter(CreateBirthdayManagerWindow()));
            _settingsHost = new SingletonWindowHost(() => new WindowAdapter(CreateSettingsWindow()));
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
            var safeToShutdown = await TryRestoreAfterStartupFailureAsync();
            System.Windows.MessageBox.Show(
                safeToShutdown
                    ? $"轻历无法启动：{exception.Message}"
                    : $"轻历启动失败，且系统时钟尚未恢复。程序将继续运行，请使用托盘中的“恢复系统时钟”后再退出。\n\n{exception.Message}",
                "轻历",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            if (safeToShutdown)
            {
                Shutdown(-1);
            }
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
        _calendarDashboardViewModel?.Dispose();
        _clockWindowController?.Dispose();
        _trayIconService?.Dispose();
        _calendarPopupWindow?.Close();
        base.OnExit(e);
    }

    private async Task InitializeSettingsAsync()
    {
        var settingsPath = Path.Combine(AppPaths.DataDirectory, "settings.json");
        _isFirstRun = !File.Exists(settingsPath);
        _settingsStore = new JsonSettingsStore(settingsPath);
        _appSettings = await _settingsStore.LoadAsync(CancellationToken.None);
    }

    private async Task<CalendarDashboardViewModel> CreateCalendarDashboardViewModelAsync()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        var connectionFactory = new SqliteConnectionFactory(AppPaths.DatabasePath);
        var migrationResult = await new DatabaseMigrator(connectionFactory).TryMigrateAsync(CancellationToken.None);

        if (!migrationResult.IsWritable)
        {
            throw new InvalidOperationException(
                migrationResult.ErrorMessage ?? "Unable to initialize the QingLi database.");
        }

        BirthdayRepository = new SqliteBirthdayRepository(connectionFactory);
        AnniversaryRepository = new SqliteAnniversaryRepository(connectionFactory);
        _reminderSuppression = new SqliteReminderSuppression(connectionFactory);
        _reminderHistory = new SqliteReminderHistoryRepository(connectionFactory);

        var holidayDefinitions = await LoadHolidayDefinitionsAsync();
        var holidayService = new HolidayService(holidayDefinitions);
        var calendarMonthService = new CalendarMonthService(
            new LunarCalendarService(),
            new SolarTermService(),
            holidayService);

        var calendar = new CalendarPopupViewModel(
            calendarMonthService,
            BirthdayRepository,
            new BirthdayOccurrenceService(),
            DateOnly.FromDateTime(DateTime.Today),
            _appSettings.FirstDayOfWeek);
        var almanac = new LunarSharpAlmanacService();
        var history = await LoadHistoryProviderAsync();
        var upcoming = new UpcomingEventService(
            new SolarTermService(),
            holidayService,
            almanac,
            BirthdayRepository,
            AnniversaryRepository,
            new BirthdayOccurrenceService(),
            new AnniversaryOccurrenceService());
        var dashboard = new CalendarDashboardViewModel(
            calendar,
            almanac,
            history,
            upcoming,
            DateOnly.FromDateTime(DateTime.Today));

        await dashboard.InitializeAsync();
        return dashboard;
    }

    private static async Task<IHistoryTodayProvider> LoadHistoryProviderAsync()
    {
        try
        {
            var bundledPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "History",
                "history-today.zh-CN.json");
            var path = new ValidatedDataPackageStore(Path.Combine(AppPaths.DataDirectory, "updates"))
                .ResolvePackagePath("history-today", bundledPath);
            return await JsonHistoryTodayProvider.LoadAsync(path);
        }
        catch
        {
            return EmptyHistoryTodayProvider.Instance;
        }
    }

    private static async Task<IReadOnlyList<HolidayDefinition>> LoadHolidayDefinitionsAsync()
    {
        try
        {
            var bundledPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Holidays", "cn-2026.json");
            var holidayPath = new ValidatedDataPackageStore(Path.Combine(AppPaths.DataDirectory, "updates"))
                .ResolvePackagePath("cn-holidays-2026", bundledPath);
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
        if (_calendarDashboardViewModel is null || _calendarPopupLayoutStore is null)
        {
            return;
        }

        if (_calendarPopupWindow is { IsVisible: true })
        {
            _calendarPopupWindow.Hide();
            return;
        }

        ShowCalendar();
    }

    private async void ShowCalendar()
    {
        if (_calendarDashboardViewModel is null || _calendarPopupLayoutStore is null)
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
            _calendarPopupWindow = new CalendarPopupWindow(_calendarDashboardViewModel, _calendarPopupLayoutStore);
            _calendarPopupWindow.AddBirthdayRequested += date => ShowBirthdayEditor(date);
            _calendarPopupWindow.AddAnniversaryRequested += date => ShowAnniversaryEditor(date);
            _calendarPopupWindow.SettingsRequested += ShowSettings;
            _calendarPopupWindow.UpcomingEventRequested += OpenUpcomingEvent;
        }

        await _calendarDashboardViewModel.SelectDateAsync(DateOnly.FromDateTime(DateTime.Today));
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

    private void ShowBirthdayEditor(DateOnly date, Birthday? birthday = null)
    {
        var viewModel = new BirthdayEditorViewModel(
            BirthdayRepository,
            birthday: birthday,
            defaultDate: date);
        viewModel.Saved += _ => RefreshDashboard();
        var window = new BirthdayEditorWindow(viewModel) { Owner = _calendarPopupWindow };
        window.ShowDialog();
    }

    private void ShowAnniversaryEditor(DateOnly date, Anniversary? anniversary = null)
    {
        var viewModel = new AnniversaryEditorViewModel(
            AnniversaryRepository,
            anniversary: anniversary,
            defaultDate: date);
        viewModel.Saved += _ => RefreshDashboard();
        var window = new AnniversaryEditorWindow(viewModel) { Owner = _calendarPopupWindow };
        window.ShowDialog();
    }

    private async void OpenUpcomingEvent(UpcomingEventViewModel item)
    {
        if (item.SubjectId is not { } id) return;
        if (item.Kind == UpcomingEventKind.Birthday)
        {
            var birthday = await BirthdayRepository.GetAsync(id, CancellationToken.None);
            if (birthday is not null) ShowBirthdayEditor(item.Date, birthday);
        }
        else if (item.Kind == UpcomingEventKind.Anniversary)
        {
            var anniversary = await AnniversaryRepository.GetAsync(id, CancellationToken.None);
            if (anniversary is not null) ShowAnniversaryEditor(item.Date, anniversary);
        }
    }

    private async void RefreshDashboard()
    {
        if (_calendarDashboardViewModel is null) return;
        var selectedDate = _calendarDashboardViewModel.SelectedDate;
        await _calendarDashboardViewModel.Calendar.LoadMonthAsync(
            _calendarDashboardViewModel.Calendar.DisplayMonth);
        await _calendarDashboardViewModel.SelectDateAsync(selectedDate);
    }

    private void ShowSettings() => _settingsHost?.Show();

    private void CreateClockReplacementServices()
    {
        if (_settingsStore is null)
        {
            throw new InvalidOperationException("Settings store is not initialized.");
        }

        var locator = new TaskbarLocator();
        _clockWindowController = new ClockWindowController(
            locator,
            () => new TaskbarClockWindow(
                new TaskbarClockViewModel(() => SystemParameters.HighContrast),
                () => _appSettings,
                ToggleCalendar),
            new Win32TaskbarWindowPositioner());
        _clockReplacementCoordinator = new ClockReplacementCoordinator(
            new Windows11TaskbarCompatibility(locator),
            new WindowsSystemClockPolicy(),
            new SystemClockStateStore(),
            _clockWindowController,
            _settingsStore);
        _exitOrchestrator = new ClockReplacementExitOrchestrator(
            _clockReplacementCoordinator,
            () => _appSettings,
            settings => _appSettings = settings,
            ShowClockReplacementWarning,
            Shutdown);
    }

    private async void RestoreSystemClock()
    {
        if (_clockReplacementCoordinator is null)
        {
            return;
        }

        var result = await _clockReplacementCoordinator.SetEnabledAsync(
            false, _appSettings, CancellationToken.None);
        _appSettings = result.Settings;
        if (!result.Succeeded)
        {
            System.Windows.MessageBox.Show(
                result.ErrorMessage ?? "无法恢复系统时钟",
                "轻历",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void ExitSafely()
    {
        if (_exitOrchestrator is not null)
        {
            await _exitOrchestrator.TryExitAsync();
        }
    }

    private async Task<bool> TryRestoreAfterStartupFailureAsync()
    {
        if (_clockReplacementCoordinator is null)
        {
            return true;
        }

        try
        {
            var result = await _clockReplacementCoordinator.SetEnabledAsync(
                false, _appSettings, CancellationToken.None);
            _appSettings = result.Settings;
            return !result.Settings.ReplaceSystemClock;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowClockReplacementWarning(string message) =>
        System.Windows.MessageBox.Show(
            message,
            "轻历",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

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
            AnniversaryRepository,
            new ReminderPlanner(new BirthdayOccurrenceService(), new AnniversaryOccurrenceService()),
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
            OpenDirectory,
            _clockReplacementCoordinator,
            settings => _appSettings = settings,
            ResetCalendarPopupLayoutAsync,
            () => _calendarPopupWindow?.LastLayoutPersistenceError?.Message);

        return new SettingsWindow(viewModel);
    }

    private async Task ResetCalendarPopupLayoutAsync()
    {
        if (_calendarPopupWindow is not null)
        {
            await _calendarPopupWindow.ResetLayoutAsync();
            return;
        }

        if (_calendarPopupLayoutStore is not null)
        {
            await _calendarPopupLayoutStore.ClearAsync(CancellationToken.None);
        }
    }

    private static void OpenDirectory(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private sealed class EmptyHistoryTodayProvider : IHistoryTodayProvider
    {
        public static EmptyHistoryTodayProvider Instance { get; } = new();

        public IReadOnlyList<HistoryTodayEntry> GetEntries(DateOnly date) => [];
    }
}
