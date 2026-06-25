using System.ComponentModel;
using System.Runtime.CompilerServices;
using QingLi.Core.Settings;
using QingLi.Windows.Startup;

namespace QingLi.Windows.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsStore _settingsStore;
    private readonly IStartupTaskService _startupTaskService;
    private readonly string _executablePath;
    private readonly Func<bool> _isHighContrast;
    private readonly Action<string> _openDirectory;
    private AppTheme _theme = AppSettings.Default.Theme;
    private DayOfWeek _firstDayOfWeek = AppSettings.Default.FirstDayOfWeek;
    private bool _startWithWindows = AppSettings.Default.StartWithWindows;
    private bool _useTwelveHourClock = AppSettings.Default.UseTwelveHourClock;
    private string _dateFormat = AppSettings.Default.DateFormat;
    private double _clockFontSize = AppSettings.Default.ClockFontSize;
    private string? _clockTextColor = AppSettings.Default.ClockTextColor;

    public SettingsViewModel(
        ISettingsStore settingsStore,
        IStartupTaskService startupTaskService,
        string executablePath,
        Func<bool> isHighContrast,
        Action<string> openDirectory)
    {
        _settingsStore = settingsStore;
        _startupTaskService = startupTaskService;
        _executablePath = executablePath;
        _isHighContrast = isHighContrast;
        _openDirectory = openDirectory;

        LoadCommand = new AsyncCommand(LoadAsync);
        SaveCommand = new AsyncCommand(SaveAsync);
        OpenDataDirectoryCommand = new AsyncCommand(OpenDataDirectoryAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand LoadCommand { get; }

    public AsyncCommand SaveCommand { get; }

    public AsyncCommand OpenDataDirectoryCommand { get; }

    public AppTheme Theme
    {
        get => _theme;
        set => SetField(ref _theme, value);
    }

    public DayOfWeek FirstDayOfWeek
    {
        get => _firstDayOfWeek;
        set => SetField(ref _firstDayOfWeek, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetField(ref _startWithWindows, value);
    }

    public bool UseTwelveHourClock
    {
        get => _useTwelveHourClock;
        set => SetField(ref _useTwelveHourClock, value);
    }

    public string DateFormat
    {
        get => _dateFormat;
        set => SetField(ref _dateFormat, value);
    }

    public double ClockFontSize
    {
        get => _clockFontSize;
        set => SetField(ref _clockFontSize, value);
    }

    public string? ClockTextColor
    {
        get => _clockTextColor;
        set
        {
            if (SetField(ref _clockTextColor, value))
            {
                OnPropertyChanged(nameof(EffectiveClockTextColor));
            }
        }
    }

    public bool IsHighContrast => _isHighContrast();

    public bool CanCustomizeClockTextColor => !IsHighContrast;

    public string EffectiveClockTextColor =>
        IsHighContrast
            ? "System"
            : string.IsNullOrWhiteSpace(ClockTextColor) ? "Default" : ClockTextColor;

    public bool CanReplaceSystemClock => false;

    public string ReplaceSystemClockMessage => "下一阶段提供";

    private async Task LoadAsync()
    {
        var settings = await _settingsStore.LoadAsync(CancellationToken.None);
        Theme = settings.Theme;
        FirstDayOfWeek = settings.FirstDayOfWeek;
        UseTwelveHourClock = settings.UseTwelveHourClock;
        DateFormat = settings.DateFormat;
        ClockFontSize = settings.ClockFontSize;
        ClockTextColor = settings.ClockTextColor;
        StartWithWindows = await Task.Run(() => _startupTaskService.IsEnabled(_executablePath));
    }

    private async Task SaveAsync()
    {
        var settings = new AppSettings(
            Theme,
            FirstDayOfWeek,
            StartWithWindows,
            UseTwelveHourClock,
            DateFormat,
            ClockFontSize,
            ClockTextColor);

        await _settingsStore.SaveAsync(settings, CancellationToken.None);
        await Task.Run(() => _startupTaskService.SetEnabled(StartWithWindows, _executablePath));
    }

    private Task OpenDataDirectoryAsync()
    {
        _openDirectory(AppPaths.DataDirectory);
        return Task.CompletedTask;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
