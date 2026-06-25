using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using QingLi.Core.Birthdays;

namespace QingLi.Windows.ViewModels;

public sealed class BirthdayManagerViewModel : INotifyPropertyChanged
{
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly BirthdayOccurrenceService _birthdayOccurrenceService;
    private readonly Func<DateOnly> _todayProvider;
    private BirthdayListItemViewModel? _selectedBirthday;
    private string? _searchText;

    public BirthdayManagerViewModel(
        IBirthdayRepository birthdayRepository,
        BirthdayOccurrenceService birthdayOccurrenceService,
        Func<DateOnly>? todayProvider = null)
    {
        _birthdayRepository = birthdayRepository;
        _birthdayOccurrenceService = birthdayOccurrenceService;
        _todayProvider = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));

        LoadCommand = new AsyncCommand(() => LoadAsync(SearchText));
        SearchCommand = new AsyncCommand(() => LoadAsync(SearchText));
        DeleteSelectedCommand = new AsyncCommand(DeleteSelectedAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BirthdayListItemViewModel> Birthdays { get; } = [];

    public AsyncCommand LoadCommand { get; }

    public AsyncCommand SearchCommand { get; }

    public AsyncCommand DeleteSelectedCommand { get; }

    public string? SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    public BirthdayListItemViewModel? SelectedBirthday
    {
        get => _selectedBirthday;
        set => SetField(ref _selectedBirthday, value);
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedBirthday is null)
        {
            return;
        }

        var deletedId = SelectedBirthday.Id;
        await _birthdayRepository.DeleteAsync(deletedId, CancellationToken.None);

        var toRemove = Birthdays.FirstOrDefault(item => item.Id == deletedId);
        if (toRemove is not null)
        {
            Birthdays.Remove(toRemove);
        }

        SelectedBirthday = Birthdays.FirstOrDefault();
    }

    private async Task LoadAsync(string? nameFilter)
    {
        var today = _todayProvider();
        var birthdays = await _birthdayRepository.ListAsync(nameFilter?.Trim(), today, CancellationToken.None);
        var orderedItems = birthdays
            .Select(birthday => BirthdayListItemViewModel.Create(
                birthday,
                GetNextOccurrence(birthday, today)))
            .OrderBy(item => item.NextOccurrenceDate)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture)
            .ToArray();

        Birthdays.Clear();
        foreach (var item in orderedItems)
        {
            Birthdays.Add(item);
        }

        SelectedBirthday = Birthdays.FirstOrDefault();
    }

    private DateOnly GetNextOccurrence(Birthday birthday, DateOnly today)
    {
        var occurrence = _birthdayOccurrenceService.GetOccurrence(birthday, today.Year);
        return occurrence >= today
            ? occurrence
            : _birthdayOccurrenceService.GetOccurrence(birthday, today.Year + 1);
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

public sealed record BirthdayListItemViewModel(
    Guid Id,
    string Name,
    string CalendarKindText,
    string BirthdayText,
    DateOnly NextOccurrenceDate,
    string NextOccurrenceText,
    string ReminderRuleText,
    Birthday Birthday)
{
    public static BirthdayListItemViewModel Create(Birthday birthday, DateOnly nextOccurrenceDate) =>
        new(
            birthday.Id,
            birthday.Name,
            birthday.CalendarKind == BirthdayCalendarKind.Gregorian ? "公历" : "农历",
            FormatBirthday(birthday),
            nextOccurrenceDate,
            nextOccurrenceDate.ToString("yyyy-MM-dd"),
            FormatReminder(birthday),
            birthday);

    private static string FormatBirthday(Birthday birthday)
    {
        var suffix = birthday.CalendarKind == BirthdayCalendarKind.Lunar && birthday.IsLeapMonth
            ? "（闰月）"
            : string.Empty;

        return $"{birthday.Month}月{birthday.Day}日{suffix}";
    }

    private static string FormatReminder(Birthday birthday) =>
        birthday.ReminderDaysBefore == 0
            ? $"当天 {birthday.ReminderTime:HH:mm}"
            : $"提前{birthday.ReminderDaysBefore}天 {birthday.ReminderTime:HH:mm}";
}
