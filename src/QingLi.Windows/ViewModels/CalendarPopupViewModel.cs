using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;

namespace QingLi.Windows.ViewModels;

public sealed class CalendarPopupViewModel : INotifyPropertyChanged
{
    private readonly CalendarMonthService _calendarMonthService;
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly BirthdayOccurrenceService _birthdayOccurrenceService;
    private readonly DateOnly _today;
    private readonly DayOfWeek _firstDayOfWeek;
    private DateOnly _displayMonth;
    private CalendarDayViewModel? _selectedDay;

    public CalendarPopupViewModel(
        CalendarMonthService calendarMonthService,
        IBirthdayRepository birthdayRepository,
        BirthdayOccurrenceService birthdayOccurrenceService,
        DateOnly today,
        DayOfWeek firstDayOfWeek)
    {
        _calendarMonthService = calendarMonthService;
        _birthdayRepository = birthdayRepository;
        _birthdayOccurrenceService = birthdayOccurrenceService;
        _today = today;
        _firstDayOfWeek = firstDayOfWeek;
        WeekdayHeaders = Enumerable.Range(0, 7)
            .Select(offset => (DayOfWeek)(((int)_firstDayOfWeek + offset) % 7))
            .Select(day => new CalendarWeekdayHeader(
                day switch
                {
                    DayOfWeek.Sunday => "日",
                    DayOfWeek.Monday => "一",
                    DayOfWeek.Tuesday => "二",
                    DayOfWeek.Wednesday => "三",
                    DayOfWeek.Thursday => "四",
                    DayOfWeek.Friday => "五",
                    DayOfWeek.Saturday => "六",
                    _ => string.Empty
                },
                day is DayOfWeek.Saturday or DayOfWeek.Sunday))
            .ToArray();

        PreviousMonthCommand = new AsyncCommand(() => LoadMonthAsync(DisplayMonth.AddMonths(-1)));
        NextMonthCommand = new AsyncCommand(() => LoadMonthAsync(DisplayMonth.AddMonths(1)));
        TodayCommand = new AsyncCommand(() => LoadMonthAsync(new DateOnly(_today.Year, _today.Month, 1)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    public IReadOnlyList<CalendarWeekdayHeader> WeekdayHeaders { get; }

    public DateOnly DisplayMonth
    {
        get => _displayMonth;
        private set
        {
            if (_displayMonth == value)
            {
                return;
            }

            _displayMonth = value;
            OnPropertyChanged();
        }
    }

    public CalendarDayViewModel? SelectedDay
    {
        get => _selectedDay;
        set
        {
            if (ReferenceEquals(_selectedDay, value))
            {
                return;
            }

            _selectedDay = value;
            OnPropertyChanged();
        }
    }

    public AsyncCommand PreviousMonthCommand { get; }

    public AsyncCommand NextMonthCommand { get; }

    public AsyncCommand TodayCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) =>
        await LoadMonthAsync(new DateOnly(_today.Year, _today.Month, 1), cancellationToken);

    public async Task LoadMonthAsync(DateOnly month, CancellationToken cancellationToken = default)
    {
        DisplayMonth = new DateOnly(month.Year, month.Month, 1);

        var birthdays = await _birthdayRepository.ListAsync(null, DisplayMonth, cancellationToken);
        var enabledBirthdays = birthdays.Where(birthday => birthday.IsEnabled).ToArray();
        var days = _calendarMonthService.Build(DisplayMonth.Year, DisplayMonth.Month, _firstDayOfWeek)
            .Select(day => new CalendarDayViewModel(day, _today))
            .ToArray();

        var yearsInGrid = days.Select(day => day.Date.Year).Distinct().ToArray();
        foreach (var birthday in enabledBirthdays)
        {
            foreach (var year in yearsInGrid)
            {
                var occurrence = _birthdayOccurrenceService.GetOccurrence(birthday, year);
                var day = days.FirstOrDefault(item => item.Date == occurrence);
                day?.Birthdays.Add(birthday.Name);
            }
        }

        Days.Clear();
        foreach (var day in days)
        {
            Days.Add(day);
        }

        SelectedDay = Days.FirstOrDefault(day => day.Date == _today)
            ?? Days.FirstOrDefault(day => day.IsCurrentMonth)
            ?? Days.FirstOrDefault();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record CalendarWeekdayHeader(string Text, bool IsWeekend);
