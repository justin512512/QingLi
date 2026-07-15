using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using QingLi.Core.Anniversaries;
using QingLi.Core.Calendars;

namespace QingLi.Windows.ViewModels;

public sealed class AnniversaryEditorViewModel : INotifyPropertyChanged
{
    private readonly IAnniversaryRepository _repository;
    private readonly Func<int, int, int, bool, bool> _lunarDateValidator;
    private readonly Guid _id;
    private AnniversaryCalendarKind _calendarKind;
    private string _title = string.Empty;
    private string _startYearText;
    private string _monthText;
    private string _dayText;
    private bool _isLeapMonth;
    private string _reminderDaysBeforeText = "0";
    private string _reminderTimeText = "09:00";
    private string? _notes;
    private bool _isEnabled = true;
    private IReadOnlyList<string> _validationErrors = [];
    private string _saveErrorMessage = string.Empty;

    public AnniversaryEditorViewModel(
        IAnniversaryRepository repository,
        Func<int, int, int, bool, bool>? lunarDateValidator = null,
        Anniversary? anniversary = null,
        DateOnly? defaultDate = null)
    {
        _repository = repository;
        _lunarDateValidator = lunarDateValidator ?? ValidateLunarDate;
        var initialDate = defaultDate ?? DateOnly.FromDateTime(DateTime.Today);
        _startYearText = initialDate.Year.ToString(CultureInfo.InvariantCulture);
        _monthText = initialDate.Month.ToString(CultureInfo.InvariantCulture);
        _dayText = initialDate.Day.ToString(CultureInfo.InvariantCulture);

        if (anniversary is null)
        {
            _id = Guid.NewGuid();
            _calendarKind = AnniversaryCalendarKind.Gregorian;
        }
        else
        {
            _id = anniversary.Id;
            _calendarKind = anniversary.CalendarKind;
            _title = anniversary.Title;
            _startYearText = anniversary.StartYear.ToString(CultureInfo.InvariantCulture);
            _monthText = anniversary.Month.ToString(CultureInfo.InvariantCulture);
            _dayText = anniversary.Day.ToString(CultureInfo.InvariantCulture);
            _isLeapMonth = anniversary.IsLeapMonth;
            _reminderDaysBeforeText = anniversary.ReminderDaysBefore.ToString(CultureInfo.InvariantCulture);
            _reminderTimeText = anniversary.ReminderTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            _notes = anniversary.Notes;
            _isEnabled = anniversary.IsEnabled;
        }

        SaveCommand = new AsyncCommand(SaveAsync);
        SaveCommand.ErrorOccurred += (_, exception) => SaveErrorMessage = exception.Message;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<Anniversary>? Saved;

    public AsyncCommand SaveCommand { get; }
    public AnniversaryCalendarKind CalendarKind { get => _calendarKind; set => SetField(ref _calendarKind, value); }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public string StartYearText { get => _startYearText; set => SetField(ref _startYearText, value); }
    public string MonthText { get => _monthText; set => SetField(ref _monthText, value); }
    public string DayText { get => _dayText; set => SetField(ref _dayText, value); }
    public bool IsLeapMonth { get => _isLeapMonth; set => SetField(ref _isLeapMonth, value); }
    public string ReminderDaysBeforeText { get => _reminderDaysBeforeText; set => SetField(ref _reminderDaysBeforeText, value); }
    public string ReminderTimeText { get => _reminderTimeText; set => SetField(ref _reminderTimeText, value); }
    public string? Notes { get => _notes; set => SetField(ref _notes, value); }
    public bool IsEnabled { get => _isEnabled; set => SetField(ref _isEnabled, value); }
    public IReadOnlyList<string> ValidationErrors { get => _validationErrors; private set => SetField(ref _validationErrors, value); }
    public string SaveErrorMessage { get => _saveErrorMessage; private set => SetField(ref _saveErrorMessage, value); }

    public IReadOnlyList<string> Validate()
    {
        _ = TryBuild(out _, out var errors);
        return errors;
    }

    private async Task SaveAsync()
    {
        SaveErrorMessage = string.Empty;
        if (!TryBuild(out var anniversary, out var errors))
        {
            ValidationErrors = errors;
            return;
        }

        ValidationErrors = [];
        await _repository.SaveAsync(anniversary!, CancellationToken.None);
        Saved?.Invoke(anniversary!);
    }

    private bool TryBuild(out Anniversary? anniversary, out IReadOnlyList<string> errors)
    {
        var validationErrors = new List<string>();
        anniversary = null;
        if (string.IsNullOrWhiteSpace(Title)) validationErrors.Add("请输入纪念日名称");

        var hasYear = TryParseRequiredInt(StartYearText, "开始年份不能为空", "开始年份必须是数字", out var year, validationErrors);
        if (hasYear && year is < 1 or > 9999) validationErrors.Add("年份应在 1 到 9999 之间");
        var hasMonth = TryParseRequiredInt(MonthText, "月份不能为空", "月份必须是数字", out var month, validationErrors);
        if (hasMonth && month is < 1 or > 12) validationErrors.Add("月份应在 1 到 12 之间");
        var hasDay = TryParseRequiredInt(DayText, "日期不能为空", "日期必须是数字", out var day, validationErrors);
        var hasLead = TryParseRequiredInt(ReminderDaysBeforeText, "提前天数不能为空", "提前天数必须是数字", out var lead, validationErrors);
        if (hasLead && lead is < 0 or > 365) validationErrors.Add("提前天数应在 0 到 365 之间");
        if (!TimeOnly.TryParse(ReminderTimeText, out var reminderTime)) validationErrors.Add("提醒时间格式无效");

        if (hasYear && hasMonth && hasDay && month is >= 1 and <= 12 && year is >= 1 and <= 9999)
        {
            if (CalendarKind == AnniversaryCalendarKind.Gregorian)
            {
                if (day < 1 || day > DateTime.DaysInMonth(year, month)) validationErrors.Add("日期超出范围");
            }
            else if (day is < 1 or > 30)
            {
                validationErrors.Add("日期超出范围");
                validationErrors.Add("农历日期应在 1 到 30 之间");
            }
            else if (!_lunarDateValidator(year, month, day, IsLeapMonth))
            {
                validationErrors.Add("农历纪念日无效");
            }
        }

        errors = validationErrors;
        if (validationErrors.Count > 0) return false;

        anniversary = new Anniversary(
            _id, Title.Trim(), CalendarKind, year, month, day, IsLeapMonth,
            lead, reminderTime, string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(), IsEnabled);
        return true;
    }

    private static bool TryParseRequiredInt(string? text, string emptyMessage, string invalidMessage, out int value, ICollection<string> errors)
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
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
