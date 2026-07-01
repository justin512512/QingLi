using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using QingLi.Core.Settings;
using QingLi.Windows.ClockReplacement;
using QingLi.Windows.Startup;

namespace QingLi.Windows.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsStore _settingsStore;
    private readonly IStartupTaskService _startupTaskService;
    private readonly string _executablePath;
    private readonly Func<bool> _isHighContrast;
    private readonly Action<string> _openDirectory;
    private readonly IClockReplacementCoordinator? _clockReplacementCoordinator;
    private readonly Action<AppSettings> _onSettingsSaved;
    private AppTheme _theme = AppSettings.Default.Theme;
    private DayOfWeek _firstDayOfWeek = AppSettings.Default.FirstDayOfWeek;
    private bool _startWithWindows = AppSettings.Default.StartWithWindows;
    private bool _useTwelveHourClock = AppSettings.Default.UseTwelveHourClock;
    private string _dateFormat = AppSettings.Default.DateFormat;
    private string _clockFontSizeText = AppSettings.Default.ClockFontSize.ToString(CultureInfo.InvariantCulture);
    private string? _clockTextColor = AppSettings.Default.ClockTextColor;
    private bool _replaceSystemClock = AppSettings.Default.ReplaceSystemClock;
    private bool _loadedReplaceSystemClock = AppSettings.Default.ReplaceSystemClock;
    private IReadOnlyList<string> _validationErrors = [];
    private string _saveErrorMessage = string.Empty;

    public SettingsViewModel(
        ISettingsStore settingsStore,
        IStartupTaskService startupTaskService,
        string executablePath,
        Func<bool> isHighContrast,
        Action<string> openDirectory,
        IClockReplacementCoordinator? clockReplacementCoordinator = null,
        Action<AppSettings>? onSettingsSaved = null)
    {
        _settingsStore = settingsStore;
        _startupTaskService = startupTaskService;
        _executablePath = executablePath;
        _isHighContrast = isHighContrast;
        _openDirectory = openDirectory;
        _clockReplacementCoordinator = clockReplacementCoordinator;
        _onSettingsSaved = onSettingsSaved ?? (_ => { });

        LoadCommand = new AsyncCommand(LoadAsync);
        SaveCommand = new AsyncCommand(SaveAsync);
        SaveCommand.ErrorOccurred += (_, exception) => SaveErrorMessage = exception.Message;
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

    public string ClockFontSizeText
    {
        get => _clockFontSizeText;
        set => SetField(ref _clockFontSizeText, value);
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

    public bool ReplaceSystemClock
    {
        get => _replaceSystemClock;
        set => SetField(ref _replaceSystemClock, value);
    }

    public IReadOnlyList<string> ValidationErrors
    {
        get => _validationErrors;
        private set => SetField(ref _validationErrors, value);
    }

    public string SaveErrorMessage
    {
        get => _saveErrorMessage;
        private set => SetField(ref _saveErrorMessage, value);
    }

    public bool CanCloseAfterSave => ValidationErrors.Count == 0 && SaveCommand.LastError is null;

    public bool IsHighContrast => _isHighContrast();

    public bool CanCustomizeClockTextColor => !IsHighContrast;

    public string EffectiveClockTextColor =>
        IsHighContrast
            ? "System"
            : string.IsNullOrWhiteSpace(ClockTextColor) ? "Default" : ClockTextColor;

    public bool CanReplaceSystemClock => _clockReplacementCoordinator?.IsCompatible == true;

    public string ReplaceSystemClockMessage =>
        _clockReplacementCoordinator?.CompatibilityMessage ?? "当前使用托盘模式";

    private async Task LoadAsync()
    {
        var settings = await _settingsStore.LoadAsync(CancellationToken.None);
        Theme = settings.Theme;
        FirstDayOfWeek = settings.FirstDayOfWeek;
        UseTwelveHourClock = settings.UseTwelveHourClock;
        DateFormat = settings.DateFormat;
        ClockFontSizeText = settings.ClockFontSize.ToString(CultureInfo.InvariantCulture);
        ClockTextColor = settings.ClockTextColor;
        ReplaceSystemClock = settings.ReplaceSystemClock;
        _loadedReplaceSystemClock = settings.ReplaceSystemClock;
        StartWithWindows = await Task.Run(() => _startupTaskService.IsEnabled(_executablePath));
    }

    private async Task SaveAsync()
    {
        SaveErrorMessage = string.Empty;

        if (!TryBuildSettings(out var settings, out var errors))
        {
            ValidationErrors = errors;
            return;
        }

        ValidationErrors = [];

        var originalStartup = await Task.Run(() => _startupTaskService.IsEnabled(_executablePath));
        var startupChanged = false;

        try
        {
            if (originalStartup != StartWithWindows)
            {
                await Task.Run(() => _startupTaskService.SetEnabled(StartWithWindows, _executablePath));
                startupChanged = true;
            }

            AppSettings savedSettings;
            if (_clockReplacementCoordinator is not null &&
                settings!.ReplaceSystemClock != _loadedReplaceSystemClock)
            {
                var result = await _clockReplacementCoordinator.SetEnabledAsync(
                    settings.ReplaceSystemClock, settings, CancellationToken.None);
                ReplaceSystemClock = result.Settings.ReplaceSystemClock;
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        result.ErrorMessage ?? "无法更新系统时钟替换状态");
                }

                savedSettings = result.Settings;
            }
            else
            {
                savedSettings = settings! with
                {
                    ReplaceSystemClock = _clockReplacementCoordinator is null
                        ? false
                        : settings.ReplaceSystemClock
                };
                await _settingsStore.SaveAsync(savedSettings, CancellationToken.None);
            }

            _loadedReplaceSystemClock = savedSettings.ReplaceSystemClock;
            _onSettingsSaved(savedSettings);
        }
        catch
        {
            if (startupChanged)
            {
                try
                {
                    await Task.Run(() => _startupTaskService.SetEnabled(originalStartup, _executablePath));
                }
                catch
                {
                    // Best effort rollback only.
                }
            }

            throw;
        }
    }

    private bool TryBuildSettings(out AppSettings? settings, out IReadOnlyList<string> errors)
    {
        var validationErrors = new List<string>();
        settings = null;

        if (string.IsNullOrWhiteSpace(ClockFontSizeText))
        {
            validationErrors.Add("时钟字号不能为空");
        }
        else if (!double.TryParse(
                     ClockFontSizeText,
                     NumberStyles.Float | NumberStyles.AllowThousands,
                     CultureInfo.InvariantCulture,
                     out var clockFontSize))
        {
            validationErrors.Add("时钟字号必须是数字");
        }
        else if (clockFontSize <= 0)
        {
            validationErrors.Add("时钟字号必须大于 0");
        }
        else
        {
            errors = validationErrors;
            settings = new AppSettings(
                Theme,
                FirstDayOfWeek,
                StartWithWindows,
                UseTwelveHourClock,
                DateFormat,
                clockFontSize,
                ClockTextColor,
                ReplaceSystemClock);
            return true;
        }

        errors = validationErrors;
        return false;
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
