using QingLi.Core.Settings;
using QingLi.Windows;
using QingLi.Windows.Startup;
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
        vm.ClockFontSize = 18;
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
        Assert.Equal(20, vm.ClockFontSize);
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
    public void Replace_system_clock_is_disabled_in_this_phase()
    {
        var vm = new SettingsViewModel(
            new RecordingSettingsStore(),
            new RecordingStartupTaskService(),
            @"C:\Apps\QingLi.exe",
            () => false,
            _ => { });

        Assert.False(vm.CanReplaceSystemClock);
        Assert.Equal("下一阶段提供", vm.ReplaceSystemClockMessage);
    }

    private sealed class RecordingSettingsStore(AppSettings? loaded = null) : ISettingsStore
    {
        private readonly AppSettings _loaded = loaded ?? AppSettings.Default;

        public List<AppSettings> SavedSettings { get; } = [];

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_loaded);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            SavedSettings.Add(settings);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStartupTaskService(bool isEnabled = false) : IStartupTaskService
    {
        private readonly bool _isEnabled = isEnabled;

        public List<(bool Enabled, string ExecutablePath)> SetCalls { get; } = [];

        public bool IsEnabled(string executablePath) => _isEnabled;

        public void SetEnabled(bool enabled, string executablePath) =>
            SetCalls.Add((enabled, executablePath));
    }
}
