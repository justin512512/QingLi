using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using QingLi.Core.Settings;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace QingLi.Windows.ViewModels;

public sealed class TaskbarClockViewModel : INotifyPropertyChanged
{
    public const string DefaultClockTextColor = "#FFF4F4F4";

    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");
    private readonly Func<bool> _isHighContrast;
    private string _timeText = string.Empty;
    private string _dateText = string.Empty;
    private double _clockFontSize = AppSettings.Default.ClockFontSize;
    private string _clockTextColor = DefaultClockTextColor;

    public TaskbarClockViewModel(Func<bool>? isHighContrast = null) =>
        _isHighContrast = isHighContrast ?? (() => false);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TimeText
    {
        get => _timeText;
        private set => SetField(ref _timeText, value);
    }

    public string DateText
    {
        get => _dateText;
        private set => SetField(ref _dateText, value);
    }

    public double ClockFontSize
    {
        get => _clockFontSize;
        private set => SetField(ref _clockFontSize, value);
    }

    public string ClockTextColor
    {
        get => _clockTextColor;
        private set => SetField(ref _clockTextColor, value);
    }

    public void Update(DateTimeOffset now, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        TimeText = now.ToString(
            settings.UseTwelveHourClock ? "h:mm tt" : "HH:mm",
            CultureInfo.InvariantCulture);
        DateText = FormatDate(now, settings.DateFormat);
        ClockFontSize = double.IsFinite(settings.ClockFontSize) &&
                        settings.ClockFontSize > 0.001 &&
                        settings.ClockFontSize <= 35_791
            ? settings.ClockFontSize
            : AppSettings.Default.ClockFontSize;
        ClockTextColor = _isHighContrast()
            ? System.Windows.SystemColors.WindowTextColor.ToString(CultureInfo.InvariantCulture)
            : NormalizeColor(settings.ClockTextColor);
    }

    private static string FormatDate(DateTimeOffset now, string? format)
    {
        try
        {
            return now.ToString(
                string.IsNullOrWhiteSpace(format) ? AppSettings.Default.DateFormat : format,
                ChineseCulture);
        }
        catch (FormatException)
        {
            return now.ToString(AppSettings.Default.DateFormat, ChineseCulture);
        }
    }

    private static string NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultClockTextColor;
        }

        try
        {
            return MediaColorConverter.ConvertFromString(value) is MediaColor color
                ? color.ToString(CultureInfo.InvariantCulture)
                : DefaultClockTextColor;
        }
        catch (FormatException)
        {
            return DefaultClockTextColor;
        }
        catch (NotSupportedException)
        {
            return DefaultClockTextColor;
        }
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
