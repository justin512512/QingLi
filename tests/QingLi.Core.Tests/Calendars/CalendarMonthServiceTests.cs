using QingLi.Core.Calendars;
using QingLi.Core.Holidays;

namespace QingLi.Core.Tests.Calendars;

public sealed class CalendarMonthServiceTests
{
    [Fact]
    public void Month_grid_contains_42_days_and_starts_on_configured_weekday()
    {
        var service = new CalendarMonthService(
            new LunarCalendarService(),
            new SolarTermService(),
            new HolidayService([]));

        var days = service.Build(2026, 6, DayOfWeek.Monday);

        Assert.Equal(42, days.Count);
        Assert.Equal(DayOfWeek.Monday, days[0].Date.DayOfWeek);
        Assert.Contains(days, x => x.Date == new DateOnly(2026, 6, 24) && x.IsCurrentMonth);
    }
}
