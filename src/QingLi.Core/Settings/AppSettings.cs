namespace QingLi.Core.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed record AppSettings(
    AppTheme Theme,
    DayOfWeek FirstDayOfWeek,
    bool StartWithWindows,
    bool UseTwelveHourClock,
    string DateFormat,
    double ClockFontSize,
    string? ClockTextColor)
{
    public static AppSettings Default { get; } = new(
        AppTheme.System,
        DayOfWeek.Monday,
        false,
        false,
        "M月d日 ddd",
        12,
        null);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
