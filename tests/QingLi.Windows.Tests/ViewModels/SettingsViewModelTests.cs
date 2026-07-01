using QingLi.Core.Settings;
using QingLi.Windows;
using QingLi.Windows.Startup;
using QingLi.Windows.ClockReplacement;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Save_command_persists_all_fields_and_updates_startup_task()
    {
        var store = new RecordingSettingsStore();
        var startup = new RecordingStartupTaskService();
        var openedPaths = new List<string>();
        var vm = new SettingsViewModel(
            store,
            startup,
            @"C:\Apps\QingLi.exe",
            () => false,
            path => openedPaths.Add(path));

        vm.Theme = AppTheme.Dark;
        vm.FirstDayOfWeek = DayOfWeek.Sunday;
        vm.StartWithWindows = true;
        vm.UseTwelveHourClock = true;
        vm.DateFormat = "yyyy/MM/dd ddd";
        vm.ClockFontSizeText = "18";
        vm.ClockTextColor = "#FF336699";

        await vm.SaveCommand.ExecuteAsync();

        var saved = Assert.Single(store.SavedSettings);
        Assert.Equal(AppTheme.Dark, saved.Theme);
        Assert.Equal(DayOfWeek.Sunday, saved.FirstDayOfWeek);
        Assert.True(saved.StartWithWindows);
        Assert.True(saved.UseTwelveHourClock);
        Assert.Equal("yyyy/MM/dd ddd", saved.DateFormat);
        Assert.Equal(18, saved.ClockFontSize);
        Assert.Equal("#FF336699", saved.ClockTextColor);
        Assert.Equal((true, @"C:\Apps\QingLi.exe"), startup.SetCalls.Single());
        Assert.Empty(openedPaths);
    }

    [Fact]
    public async Task Save_command_does_not_write_settings_when_clock_font_size_is_not_numeric()
    {
        var store = new RecordingSettingsStore();
        var startup = new RecordingStartupTaskService();
        var vm = new SettingsViewModel(
            store,
            startup,
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { });

        vm.ClockFontSizeText = "x";

        await vm.SaveCommand.ExecuteAsync();

        Assert.Empty(store.SavedSettings);
        Assert.Empty(startup.SetCalls);
        Assert.Contains("时钟字号必须是数字", vm.ValidationErrors);
        Assert.False(vm.CanCloseAfterSave);
    }

    [Fact]
    public async Task Load_command_uses_store_and_current_startup_state()
    {
        var expected = AppSettings.Default with
        {
            Theme = AppTheme.Light,
            FirstDayOfWeek = DayOfWeek.Friday,
            StartWithWindows = false,
            UseTwelveHourClock = true,
            DateFormat = "MM-dd",
            ClockFontSize = 20,
            ClockTextColor = "#FF0000"
        };

        var vm = new SettingsViewModel(
            new RecordingSettingsStore(expected),
            new RecordingStartupTaskService(isEnabled: true),
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { });

        await vm.LoadCommand.ExecuteAsync();

        Assert.Equal(AppTheme.Light, vm.Theme);
        Assert.Equal(DayOfWeek.Friday, vm.FirstDayOfWeek);
        Assert.True(vm.StartWithWindows);
        Assert.True(vm.UseTwelveHourClock);
        Assert.Equal("MM-dd", vm.DateFormat);
        Assert.Equal("20", vm.ClockFontSizeText);
        Assert.Equal("#FF0000", vm.ClockTextColor);
    }

    [Fact]
    public void High_contrast_disables_custom_color_and_uses_system_color()
    {
        var vm = new SettingsViewModel(
            new RecordingSettingsStore(),
            new RecordingStartupTaskService(),
            @"C:\Apps\QingLi.exe",
            () => true,
            _ => { });

        Assert.True(vm.IsHighContrast);
        Assert.False(vm.CanCustomizeClockTextColor);
        Assert.Equal("System", vm.EffectiveClockTextColor);
    }

    [Fact]
    public async Task Open_data_directory_command_uses_app_paths_data_directory()
    {
        var openedPaths = new List<string>();
        var vm = new SettingsViewModel(
            new RecordingSettingsStore(),
            new RecordingStartupTaskService(),
            @"C:\Apps\QingLi.exe",
            () => false,
            path => openedPaths.Add(path));

        await vm.OpenDataDirectoryCommand.ExecuteAsync();

        Assert.Equal([AppPaths.DataDirectory], openedPaths);
    }

    [Fact]
    public void Replace_system_clock_is_hidden_without_compatible_coordinator()
    {
        var vm = new SettingsViewModel(
            new RecordingSettingsStore(),
            new RecordingStartupTaskService(),
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { });

        Assert.False(vm.CanReplaceSystemClock);
        Assert.Equal("当前使用托盘模式", vm.ReplaceSystemClockMessage);
    }

    [Fact]
    public async Task Compatible_clock_toggle_is_sent_to_coordinator_and_saved_result_is_published()
    {
        var coordinator = new RecordingClockReplacementCoordinator();
        AppSettings? published = null;
        var vm = new SettingsViewModel(
            new RecordingSettingsStore(),
            new RecordingStartupTaskService(),
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { },
            coordinator,
            settings => published = settings);

        await vm.LoadCommand.ExecuteAsync();
        vm.ReplaceSystemClock = true;
        await vm.SaveCommand.ExecuteAsync();

        Assert.True(vm.CanReplaceSystemClock);
        Assert.True(Assert.Single(coordinator.Requests).Enabled);
        Assert.True(published?.ReplaceSystemClock);
        Assert.Null(vm.SaveCommand.LastError);
    }

    [Fact]
    public async Task Clock_replacement_failure_is_visible_and_keeps_settings_window_open()
    {
        var coordinator = new RecordingClockReplacementCoordinator
        {
            ResultFactory = settings => new(false, settings with { ReplaceSystemClock = false }, "替换失败")
        };
        var vm = new SettingsViewModel(
            new RecordingSettingsStore(),
            new RecordingStartupTaskService(),
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { },
            coordinator);

        await vm.LoadCommand.ExecuteAsync();
        vm.ReplaceSystemClock = true;
        await vm.SaveCommand.ExecuteAsync();

        Assert.Equal("替换失败", vm.SaveErrorMessage);
        Assert.False(vm.ReplaceSystemClock);
        Assert.False(vm.CanCloseAfterSave);
    }

    [Fact]
    public async Task Startup_failure_prevents_settings_write()
    {
        var store = new RecordingSettingsStore();
        var startup = new RecordingStartupTaskService
        {
            SetException = new InvalidOperationException("启动项失败")
        };
        var vm = new SettingsViewModel(
            store,
            startup,
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { })
        {
            StartWithWindows = true
        };

        await vm.SaveCommand.ExecuteAsync();

        Assert.Empty(store.SavedSettings);
        Assert.Equal("启动项失败", vm.SaveErrorMessage);
        Assert.NotNull(vm.SaveCommand.LastError);
    }

    [Fact]
    public async Task Save_failure_rolls_back_startup_state()
    {
        var store = new RecordingSettingsStore
        {
            SaveException = new InvalidOperationException("设置保存失败")
        };
        var startup = new RecordingStartupTaskService(isEnabled: false);
        var vm = new SettingsViewModel(
            store,
            startup,
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { })
        {
            StartWithWindows = true
        };

        await vm.SaveCommand.ExecuteAsync();

        Assert.Equal(
            [
                (true, @"C:\Apps\QingLi.exe"),
                (false, @"C:\Apps\QingLi.exe")
            ],
            startup.SetCalls);
        Assert.Equal("设置保存失败", vm.SaveErrorMessage);
        Assert.NotNull(vm.SaveCommand.LastError);
    }

    private sealed class RecordingSettingsStore(AppSettings? loaded = null) : ISettingsStore
    {
        private readonly AppSettings _loaded = loaded ?? AppSettings.Default;

        public List<AppSettings> SavedSettings { get; } = [];

        public Exception? SaveException { get; set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_loaded);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedSettings.Add(settings);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStartupTaskService(bool isEnabled = false) : IStartupTaskService
    {
        private bool _isEnabled = isEnabled;

        public List<(bool Enabled, string ExecutablePath)> SetCalls { get; } = [];

        public Exception? SetException { get; set; }

        public bool IsEnabled(string executablePath) => _isEnabled;

        public void SetEnabled(bool enabled, string executablePath)
        {
            if (SetException is not null)
            {
                throw SetException;
            }

            _isEnabled = enabled;
            SetCalls.Add((enabled, executablePath));
        }
    }

    private sealed class RecordingClockReplacementCoordinator : IClockReplacementCoordinator
    {
        public List<(bool Enabled, AppSettings Settings)> Requests { get; } = [];
        public Func<AppSettings, ClockReplacementResult>? ResultFactory { get; set; }
        public bool IsCompatible => true;
        public string CompatibilityMessage => "可用";

        public Task<ClockReplacementResult> SetEnabledAsync(
            bool enabled, AppSettings settings, CancellationToken cancellationToken)
        {
            Requests.Add((enabled, settings));
            return Task.FromResult(ResultFactory?.Invoke(settings) ??
                new ClockReplacementResult(true, settings with { ReplaceSystemClock = enabled }));
        }

        public Task<ClockReplacementResult> RecoverOnStartupAsync(
            AppSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new ClockReplacementResult(true, settings));
    }
}
