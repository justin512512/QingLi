using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.Holidays;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class CalendarPopupViewModelTests
{
    [Fact]
    public async Task Next_month_rebuilds_calendar()
    {
        var vm = CalendarPopupFixture.Create(today: new DateOnly(2026, 6, 24));

        await vm.InitializeAsync();
        await vm.NextMonthCommand.ExecuteAsync();

        Assert.Equal(7, vm.DisplayMonth.Month);
        Assert.Equal(42, vm.Days.Count);
    }

    [Fact]
    public async Task Birthday_occurrence_is_marked_on_selected_day()
    {
        var birthday = new Birthday(
            Guid.NewGuid(),
            "小林",
            BirthdayCalendarKind.Gregorian,
            1990,
            6,
            24,
            false,
            3,
            new TimeOnly(9, 0),
            null,
            true);

        var vm = CalendarPopupFixture.Create(
            today: new DateOnly(2026, 6, 24),
            birthdays: [birthday]);

        await vm.InitializeAsync();

        var selectedDay = Assert.Single(vm.Days, day => day.Date == new DateOnly(2026, 6, 24));
        Assert.Contains("小林", selectedDay.Birthdays);
        Assert.NotNull(vm.SelectedDay);
        Assert.Contains("小林", vm.SelectedDay!.Birthdays);
    }

    [Fact]
    public void Weekday_headers_follow_configured_first_day()
    {
        var vm = CalendarPopupFixture.Create(
            today: new DateOnly(2026, 6, 24),
            firstDayOfWeek: DayOfWeek.Sunday);

        Assert.Equal(["日", "一", "二", "三", "四", "五", "六"], vm.WeekdayHeaders.Select(day => day.Text));
        Assert.True(vm.WeekdayHeaders[0].IsWeekend);
    }

    private sealed class CalendarPopupFixture
    {
        private CalendarPopupFixture() { }

        public static CalendarPopupViewModel Create(
            DateOnly today,
            IReadOnlyList<Birthday>? birthdays = null,
            DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        {
            var calendarMonthService = new CalendarMonthService(
                new LunarCalendarService(),
                new SolarTermService(),
                new HolidayService([]));

            return new CalendarPopupViewModel(
                calendarMonthService,
                new BirthdayRepository(birthdays ?? []),
                new BirthdayOccurrenceService(),
                today,
                firstDayOfWeek);
        }
    }

    private sealed class BirthdayRepository(IReadOnlyList<Birthday> birthdays) : IBirthdayRepository
    {
        public Task<IReadOnlyList<Birthday>> ListAsync(
            string? nameFilter,
            DateOnly today,
            CancellationToken cancellationToken) =>
            Task.FromResult(birthdays);

        public Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Birthday?>(birthdays.FirstOrDefault(birthday => birthday.Id == id));

        public Task SaveAsync(Birthday birthday, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
