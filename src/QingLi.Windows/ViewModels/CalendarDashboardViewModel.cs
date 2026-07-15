using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using QingLi.Core.Almanac;
using QingLi.Core.History;
using QingLi.Core.Upcoming;

namespace QingLi.Windows.ViewModels;

public sealed class CalendarDashboardViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAlmanacService _almanacService;
    private readonly IHistoryTodayProvider _historyProvider;
    private readonly IUpcomingEventService _upcomingEventService;
    private readonly DateOnly _today;
    private readonly Dictionary<DateOnly, AlmanacSummaryViewModel> _almanacCache = [];
    private readonly Dictionary<(int Month, int Day), IReadOnlyList<HistoryTodayItemViewModel>> _historyCache = [];
    private CancellationTokenSource? _selectionCancellation;
    private long _selectionVersion;
    private DateOnly _selectedDate;
    private AlmanacSummaryViewModel _almanac;
    private string? _errorMessage;

    public CalendarDashboardViewModel(
        CalendarPopupViewModel calendar,
        IAlmanacService almanacService,
        IHistoryTodayProvider historyProvider,
        IUpcomingEventService upcomingEventService,
        DateOnly today)
    {
        Calendar = calendar;
        _almanacService = almanacService;
        _historyProvider = historyProvider;
        _upcomingEventService = upcomingEventService;
        _today = today;
        _selectedDate = today;
        _almanac = GetAlmanac(today);

        PreviousMonthCommand = new AsyncCommand(() => ChangeMonthAsync(-1));
        NextMonthCommand = new AsyncCommand(() => ChangeMonthAsync(1));
        TodayCommand = new AsyncCommand(GoToTodayAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CalendarPopupViewModel Calendar { get; }
    public ObservableCollection<HistoryTodayItemViewModel> HistoryToday { get; } = [];
    public ObservableCollection<UpcomingEventViewModel> UpcomingEvents { get; } = [];
    public AsyncCommand PreviousMonthCommand { get; }
    public AsyncCommand NextMonthCommand { get; }
    public AsyncCommand TodayCommand { get; }

    public DateOnly SelectedDate
    {
        get => _selectedDate;
        private set
        {
            if (_selectedDate == value) return;
            _selectedDate = value;
            OnPropertyChanged();
        }
    }

    public AlmanacSummaryViewModel Almanac
    {
        get => _almanac;
        private set
        {
            if (ReferenceEquals(_almanac, value)) return;
            _almanac = value;
            OnPropertyChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Calendar.InitializeAsync(cancellationToken);
        await SelectDateAsync(_today, cancellationToken);
    }

    public async Task SelectDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _selectionVersion);
        _selectionCancellation?.Cancel();
        _selectionCancellation?.Dispose();
        _selectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _selectionCancellation.Token;

        SelectedDate = date;
        Calendar.SelectedDay = Calendar.Days.FirstOrDefault(day => day.Date == date);
        Almanac = GetAlmanac(date);
        Replace(HistoryToday, GetHistory(date));
        ErrorMessage = null;

        try
        {
            var upcoming = await _upcomingEventService.GetUpcomingAsync(date, 90, token);
            if (version != Volatile.Read(ref _selectionVersion) || token.IsCancellationRequested)
            {
                return;
            }

            Replace(UpcomingEvents, upcoming.Select(item => new UpcomingEventViewModel(item)));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (version == Volatile.Read(ref _selectionVersion))
            {
                UpcomingEvents.Clear();
                ErrorMessage = $"近期事件暂时无法加载：{exception.Message}";
            }
        }
    }

    public void Dispose()
    {
        _selectionCancellation?.Cancel();
        _selectionCancellation?.Dispose();
    }

    private async Task ChangeMonthAsync(int offset)
    {
        await Calendar.LoadMonthAsync(Calendar.DisplayMonth.AddMonths(offset));
        var selected = Calendar.SelectedDay?.Date
            ?? new DateOnly(Calendar.DisplayMonth.Year, Calendar.DisplayMonth.Month, 1);
        await SelectDateAsync(selected);
    }

    private async Task GoToTodayAsync()
    {
        await Calendar.LoadMonthAsync(new DateOnly(_today.Year, _today.Month, 1));
        await SelectDateAsync(_today);
    }

    private AlmanacSummaryViewModel GetAlmanac(DateOnly date)
    {
        if (!_almanacCache.TryGetValue(date, out var value))
        {
            value = new AlmanacSummaryViewModel(_almanacService.GetDay(date));
            _almanacCache.Add(date, value);
        }

        return value;
    }

    private IReadOnlyList<HistoryTodayItemViewModel> GetHistory(DateOnly date)
    {
        var key = (date.Month, date.Day);
        if (!_historyCache.TryGetValue(key, out var value))
        {
            value = _historyProvider.GetEntries(date)
                .Select(item => new HistoryTodayItemViewModel(item))
                .ToArray();
            _historyCache.Add(key, value);
        }

        return value;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
