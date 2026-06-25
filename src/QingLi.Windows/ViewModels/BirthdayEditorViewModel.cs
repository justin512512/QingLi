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
    private int _birthYear = DateTime.Today.Year;
    private int _month = 1;
    private int _day = 1;
    private bool _isLeapMonth;
    private int _reminderDaysBefore;
    private string _reminderTimeText = "09:00";
    private string? _notes;
    private bool _isEnabled = true;
    private IReadOnlyList<string> _validationErrors = [];

    public BirthdayEditorViewModel(
        IBirthdayRepository birthdayRepository,
        Func<int, int, int, bool, bool>? lunarDateValidator = null,
        Birthday? birthday = null)
    {
        _birthdayRepository = birthdayRepository;
        _lunarDateValidator = lunarDateValidator ?? ValidateLunarDate;

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
            _birthYear = birthday.BirthYear;
            _month = birthday.Month;
            _day = birthday.Day;
            _isLeapMonth = birthday.IsLeapMonth;
            _reminderDaysBefore = birthday.ReminderDaysBefore;
            _reminderTimeText = birthday.ReminderTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            _notes = birthday.Notes;
            _isEnabled = birthday.IsEnabled;
        }

        SaveCommand = new AsyncCommand(SaveAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public int BirthYear
    {
        get => _birthYear;
        set => SetField(ref _birthYear, value);
    }

    public int Month
    {
        get => _month;
        set => SetField(ref _month, value);
    }

    public int Day
    {
        get => _day;
        set => SetField(ref _day, value);
    }

    public bool IsLeapMonth
    {
        get => _isLeapMonth;
        set => SetField(ref _isLeapMonth, value);
    }

    public int ReminderDaysBefore
    {
        get => _reminderDaysBefore;
        set => SetField(ref _reminderDaysBefore, value);
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

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("请输入姓名");
        }

        if (BirthYear is < 1 or > 9999)
        {
            errors.Add("年份应在 1 到 9999 之间");
        }

        if (Month is < 1 or > 12)
        {
            errors.Add("月份应在 1 到 12 之间");
        }

        if (ReminderDaysBefore is < 0 or > 365)
        {
            errors.Add("提前天数应在 0 到 365 之间");
        }

        if (!TimeOnly.TryParse(ReminderTimeText, out _))
        {
            errors.Add("提醒时间格式无效");
        }

        if (Month is >= 1 and <= 12 && BirthYear is >= 1 and <= 9999)
        {
            if (CalendarKind == BirthdayCalendarKind.Gregorian)
            {
                var maxDay = DateTime.DaysInMonth(BirthYear, Month);
                if (Day < 1 || Day > maxDay)
                {
                    errors.Add("日期超出范围");
                }
            }
            else
            {
                if (Day is < 1 or > 30)
                {
                    errors.Add("日期超出范围");
                    errors.Add("农历日期应在 1 到 30 之间");
                }
                else if (!_lunarDateValidator(BirthYear, Month, Day, IsLeapMonth))
                {
                    errors.Add("农历生日无效");
                }
            }
        }

        return errors;
    }

    private async Task SaveAsync()
    {
        ValidationErrors = Validate();
        if (ValidationErrors.Count > 0)
        {
            return;
        }

        if (!TimeOnly.TryParse(ReminderTimeText, out var reminderTime))
        {
            ValidationErrors = ["提醒时间格式无效"];
            return;
        }

        var birthday = new Birthday(
            _birthdayId,
            Name.Trim(),
            CalendarKind,
            BirthYear,
            Month,
            Day,
            IsLeapMonth,
            ReminderDaysBefore,
            reminderTime,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            IsEnabled);

        await _birthdayRepository.SaveAsync(birthday, CancellationToken.None);
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

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
