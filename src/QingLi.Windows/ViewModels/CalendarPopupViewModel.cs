using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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

        PreviousMonthCommand = new RelayCommand(() => LoadMonthAsync(DisplayMonth.AddMonths(-1)).GetAwaiter().GetResult());
        NextMonthCommand = new RelayCommand(() => LoadMonthAsync(DisplayMonth.AddMonths(1)).GetAwaiter().GetResult());
        TodayCommand = new RelayCommand(() => LoadMonthAsync(new DateOnly(_today.Year, _today.Month, 1)).GetAwaiter().GetResult());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

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

    public ICommand PreviousMonthCommand { get; }

    public ICommand NextMonthCommand { get; }

    public ICommand TodayCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) =>
        await LoadMonthAsync(new DateOnly(_today.Year, _today.Month, 1), cancellationToken);

    public async Task LoadMonthAsync(DateOnly month, CancellationToken cancellationToken = default)
    {
        DisplayMonth = new DateOnly(month.Year, month.Month, 1);

        var birthdays = await _birthdayRepository.ListAsync(null, DisplayMonth, cancellationToken)
            .ConfigureAwait(false);
        var enabledBirthdays = birthdays.Where(birthday => birthday.IsEnabled).ToArray();
        var days = _calendarMonthService.Build(DisplayMonth.Year, DisplayMonth.Month, _firstDayOfWeek)
            .Select(day => new CalendarDayViewModel(day))
            .ToArray();

        foreach (var birthday in enabledBirthdays)
        {
            var occurrence = _birthdayOccurrenceService.GetOccurrence(birthday, DisplayMonth.Year);
            if (occurrence.Year != DisplayMonth.Year || occurrence.Month != DisplayMonth.Month)
            {
                continue;
            }

            var day = days.FirstOrDefault(item => item.Date == occurrence);
            day?.Birthdays.Add(birthday.Name);
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

    private sealed class RelayCommand(Action execute) : ICommand
    {
        event EventHandler? ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
