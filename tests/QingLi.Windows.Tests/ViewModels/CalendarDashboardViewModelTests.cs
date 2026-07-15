using QingLi.Core.Almanac;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.History;
using QingLi.Core.Holidays;
using QingLi.Core.Upcoming;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class CalendarDashboardViewModelTests
{
    [Fact]
    public async Task InitializePopulatesAllThreeColumnsForToday()
    {
        var today = new DateOnly(2026, 7, 15);
        var dashboard = Create(today);

        await dashboard.InitializeAsync();

        Assert.Equal(today, dashboard.SelectedDate);
        Assert.Equal("六月初二", dashboard.Almanac.LunarMonthDay);
        Assert.Equal("历史事件", Assert.Single(dashboard.HistoryToday).Summary);
        Assert.Equal("大暑", Assert.Single(dashboard.UpcomingEvents).Title);
        Assert.Equal(42, dashboard.Calendar.Days.Count);
    }

    [Fact]
    public async Task SelectingDateUpdatesSummaryHistoryAndUpcomingEvents()
    {
        var today = new DateOnly(2026, 7, 15);
        var dashboard = Create(today);
        await dashboard.InitializeAsync();

        await dashboard.SelectDateAsync(new DateOnly(2026, 7, 16));

        Assert.Equal(new DateOnly(2026, 7, 16), dashboard.SelectedDate);
        Assert.Equal("7月16日事件", Assert.Single(dashboard.HistoryToday).Summary);
        Assert.Equal("7月16日事件", Assert.Single(dashboard.UpcomingEvents).Title);
    }

    [Fact]
    public async Task SlowerOldSelectionCannotOverwriteNewSelection()
    {
        var today = new DateOnly(2026, 7, 15);
        var upcoming = new DelayedUpcomingService();
        var dashboard = Create(today, upcoming);
        await dashboard.InitializeAsync();
        var oldDate = new DateOnly(2026, 7, 16);
        var newDate = new DateOnly(2026, 7, 17);

        var oldRequest = dashboard.SelectDateAsync(oldDate);
        await upcoming.WaitUntilDelayedRequestStartsAsync();
        await dashboard.SelectDateAsync(newDate);
        upcoming.CompleteDelayedRequest();
        await oldRequest;

        Assert.Equal(newDate, dashboard.SelectedDate);
        Assert.Equal("7月17日事件", Assert.Single(dashboard.UpcomingEvents).Title);
    }

    [Fact]
    public async Task UpcomingFailureKeepsCalendarAndShowsLocalError()
    {
        var today = new DateOnly(2026, 7, 15);
        var dashboard = Create(today, new ThrowingUpcomingService());

        await dashboard.InitializeAsync();

        Assert.Equal(42, dashboard.Calendar.Days.Count);
        Assert.Equal(today, dashboard.SelectedDate);
        Assert.NotNull(dashboard.ErrorMessage);
        Assert.Empty(dashboard.UpcomingEvents);
    }

    private static CalendarDashboardViewModel Create(
        DateOnly today,
        IUpcomingEventService? upcoming = null)
    {
        var calendar = new CalendarPopupViewModel(
            new CalendarMonthService(
                new LunarCalendarService(),
                new SolarTermService(),
                new HolidayService()),
            new BirthdayRepository(),
            new BirthdayOccurrenceService(),
            today,
            DayOfWeek.Monday);

        return new CalendarDashboardViewModel(
            calendar,
            new AlmanacService(),
            new HistoryProvider(),
            upcoming ?? new UpcomingService(),
            today);
    }

    private sealed class BirthdayRepository : IBirthdayRepository
    {
        public Task<IReadOnlyList<Birthday>> ListAsync(string? nameFilter, DateOnly today, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Birthday>>([]);
        public Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Birthday?>(null);
        public Task SaveAsync(Birthday birthday, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AlmanacService : IAlmanacService
    {
        public AlmanacDay GetDay(DateOnly date) => new(
            date, date.Day == 15 ? "六月初二" : "六月初三",
            "丙午", "乙未", "庚寅", "马", null, [], ["开市"], ["入宅"]);
    }

    private sealed class HistoryProvider : IHistoryTodayProvider
    {
        public IReadOnlyList<HistoryTodayEntry> GetEntries(DateOnly date) =>
            [new(2000, date.Day == 15 ? "历史事件" : $"{date.Month}月{date.Day}日事件", "Wikipedia", "https://zh.wikipedia.org/")];
    }

    private sealed class UpcomingService : IUpcomingEventService
    {
        public Task<IReadOnlyList<UpcomingEvent>> GetUpcomingAsync(DateOnly today, int horizonDays, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UpcomingEvent>>([
                new(today.AddDays(8), 8, UpcomingEventKind.SolarTerm, today.Day == 15 ? "大暑" : $"{today.Month}月{today.Day}日事件")
            ]);
    }

    private sealed class ThrowingUpcomingService : IUpcomingEventService
    {
        public Task<IReadOnlyList<UpcomingEvent>> GetUpcomingAsync(DateOnly today, int horizonDays, CancellationToken cancellationToken) =>
            throw new InvalidDataException("upcoming unavailable");
    }

    private sealed class DelayedUpcomingService : IUpcomingEventService
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<UpcomingEvent>> GetUpcomingAsync(DateOnly today, int horizonDays, CancellationToken cancellationToken)
        {
            if (today.Day == 16)
            {
                _started.TrySetResult();
                await _completion.Task;
            }

            return [new(today, 0, UpcomingEventKind.Festival, $"{today.Month}月{today.Day}日事件")];
        }

        public Task WaitUntilDelayedRequestStartsAsync() => _started.Task;
        public void CompleteDelayedRequest() => _completion.TrySetResult();
    }
}
