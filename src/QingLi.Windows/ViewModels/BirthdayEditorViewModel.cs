using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;

namespace QingLi.Windows.ViewModels;

public sealed class BirthdayEditorViewModel : INotifyPropertyChanged
{
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly Func<int, int, int, bool, bool> _lunarDateValidator;
    private readonly Guid _birthdayId;
    private BirthdayCalendarKind _calendarKind;
    private string _name = string.Empty;
    private string _birthYearText = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
    private string _monthText = "1";
    private string _dayText = "1";
    private bool _isLeapMonth;
    private string _reminderDaysBeforeText = "0";
    private string _reminderTimeText = "09:00";
    private string? _notes;
    private bool _isEnabled = true;
    private IReadOnlyList<string> _validationErrors = [];
    private string _saveErrorMessage = string.Empty;

    public BirthdayEditorViewModel(
        IBirthdayRepository birthdayRepository,
        Func<int, int, int, bool, bool>? lunarDateValidator = null,
        Birthday? birthday = null,
        DateOnly? defaultDate = null)
    {
        _birthdayRepository = birthdayRepository;
        _lunarDateValidator = lunarDateValidator ?? ValidateLunarDate;
        var initialDate = defaultDate ?? DateOnly.FromDateTime(DateTime.Today);
        _birthYearText = initialDate.Year.ToString(CultureInfo.InvariantCulture);
        _monthText = initialDate.Month.ToString(CultureInfo.InvariantCulture);
        _dayText = initialDate.Day.ToString(CultureInfo.InvariantCulture);

        if (birthday is null)
        {
            _birthdayId = Guid.NewGuid();
            _calendarKind = BirthdayCalendarKind.Gregorian;
        }
        else
        {
            _birthdayId = birthday.Id;
            _calendarKind = birthday.CalendarKind;
            _name = birthday.Name;
            _birthYearText = birthday.BirthYear.ToString(CultureInfo.InvariantCulture);
            _monthText = birthday.Month.ToString(CultureInfo.InvariantCulture);
            _dayText = birthday.Day.ToString(CultureInfo.InvariantCulture);
            _isLeapMonth = birthday.IsLeapMonth;
            _reminderDaysBeforeText = birthday.ReminderDaysBefore.ToString(CultureInfo.InvariantCulture);
            _reminderTimeText = birthday.ReminderTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            _notes = birthday.Notes;
            _isEnabled = birthday.IsEnabled;
        }

        SaveCommand = new AsyncCommand(SaveAsync);
        SaveCommand.ErrorOccurred += (_, exception) => SaveErrorMessage = exception.Message;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<Birthday>? Saved;

    public AsyncCommand SaveCommand { get; }

    public BirthdayCalendarKind CalendarKind
    {
        get => _calendarKind;
        set => SetField(ref _calendarKind, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string BirthYearText
    {
        get => _birthYearText;
        set => SetField(ref _birthYearText, value);
    }

    public string MonthText
    {
        get => _monthText;
        set => SetField(ref _monthText, value);
    }

    public string DayText
    {
        get => _dayText;
        set => SetField(ref _dayText, value);
    }

    public bool IsLeapMonth
    {
        get => _isLeapMonth;
        set => SetField(ref _isLeapMonth, value);
    }

    public string ReminderDaysBeforeText
    {
        get => _reminderDaysBeforeText;
        set => SetField(ref _reminderDaysBeforeText, value);
    }

    public string ReminderTimeText
    {
        get => _reminderTimeText;
        set => SetField(ref _reminderTimeText, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
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

    public IReadOnlyList<string> Validate()
    {
        _ = TryBuildBirthday(out _, out var errors);
        return errors;
    }

    private async Task SaveAsync()
    {
        SaveErrorMessage = string.Empty;

        if (!TryBuildBirthday(out var birthday, out var errors))
        {
            ValidationErrors = errors;
            return;
        }

        ValidationErrors = [];
        await _birthdayRepository.SaveAsync(birthday!, CancellationToken.None);
        Saved?.Invoke(birthday!);
    }

    private bool TryBuildBirthday(out Birthday? birthday, out IReadOnlyList<string> errors)
    {
        var validationErrors = new List<string>();
        birthday = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            validationErrors.Add("请输入姓名");
        }

        var hasBirthYear = TryParseRequiredInt(
            BirthYearText,
            "出生年不能为空",
            "出生年必须是数字",
            out var birthYear,
            validationErrors);
        if (hasBirthYear && (birthYear < 1 || birthYear > 9999))
        {
            validationErrors.Add("年份应在 1 到 9999 之间");
        }

        var hasMonth = TryParseRequiredInt(
            MonthText,
            "月份不能为空",
            "月份必须是数字",
            out var month,
            validationErrors);
        if (hasMonth && (month < 1 || month > 12))
        {
            validationErrors.Add("月份应在 1 到 12 之间");
        }

        var hasDay = TryParseRequiredInt(
            DayText,
            "日期不能为空",
            "日期必须是数字",
            out var day,
            validationErrors);

        var hasReminderDaysBefore = TryParseRequiredInt(
            ReminderDaysBeforeText,
            "提前天数不能为空",
            "提前天数必须是数字",
            out var reminderDaysBefore,
            validationErrors);
        if (hasReminderDaysBefore && (reminderDaysBefore < 0 || reminderDaysBefore > 365))
        {
            validationErrors.Add("提前天数应在 0 到 365 之间");
        }

        if (!TimeOnly.TryParse(ReminderTimeText, out var reminderTime))
        {
            validationErrors.Add("提醒时间格式无效");
        }

        if (hasMonth && hasDay && hasBirthYear)
        {
            if (CalendarKind == BirthdayCalendarKind.Gregorian)
            {
                if (month is >= 1 and <= 12)
                {
                    var maxDay = DateTime.DaysInMonth(Math.Clamp(birthYear, 1, 9999), month);
                    if (day < 1 || day > maxDay)
                    {
                        validationErrors.Add("日期超出范围");
                    }
                }
            }
            else
            {
                if (day < 1 || day > 30)
                {
                    validationErrors.Add("日期超出范围");
                    validationErrors.Add("农历日期应在 1 到 30 之间");
                }
                else if (month is >= 1 and <= 12 &&
                         birthYear is >= 1 and <= 9999 &&
                         !_lunarDateValidator(birthYear, month, day, IsLeapMonth))
                {
                    validationErrors.Add("农历生日无效");
                }
            }
        }

        errors = validationErrors;
        if (validationErrors.Count > 0)
        {
            return false;
        }

        birthday = new Birthday(
            _birthdayId,
            Name.Trim(),
            CalendarKind,
            birthYear,
            month,
            day,
            IsLeapMonth,
            reminderDaysBefore,
            reminderTime,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            IsEnabled);
        return true;
    }

    private static bool TryParseRequiredInt(
        string? text,
        string emptyMessage,
        string invalidMessage,
        out int value,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add(emptyMessage);
            value = default;
            return false;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            errors.Add(invalidMessage);
            return false;
        }

        return true;
    }

    private static bool ValidateLunarDate(int year, int month, int day, bool isLeapMonth)
    {
        try
        {
            _ = new LunarCalendarService().ToGregorian(year, month, day, isLeapMonth);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
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
