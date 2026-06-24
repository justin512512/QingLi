using QingLi.Core.Holidays;

namespace QingLi.Core.Calendars;

public sealed class CalendarMonthService(
    LunarCalendarService lunar,
    SolarTermService solarTerms,
    HolidayService holidays)
{
    public IReadOnlyList<CalendarDay> Build(int year, int month, DayOfWeek firstDay)
    {
        var first = new DateOnly(year, month, 1);
        var offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
        var start = first.AddDays(-offset);

        return Enumerable.Range(0, 42)
            .Select(index =>
            {
                var date = start.AddDays(index);
                return new CalendarDay(
                    date,
                    lunar.FromGregorian(date),
                    solarTerms.GetName(date),
                    holidays.Find(date),
                    date.Month == month);
            })
            .ToArray();
    }
}
