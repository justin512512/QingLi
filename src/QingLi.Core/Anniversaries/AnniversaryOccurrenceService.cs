using QingLi.Core.Calendars;

namespace QingLi.Core.Anniversaries;

public sealed class AnniversaryOccurrenceService
{
    private readonly LunarCalendarService _lunarCalendarService;

    public AnniversaryOccurrenceService(LunarCalendarService? lunarCalendarService = null)
    {
        _lunarCalendarService = lunarCalendarService ?? new LunarCalendarService();
    }

    public DateOnly GetOccurrence(Anniversary anniversary, int year)
    {
        if (anniversary.CalendarKind == AnniversaryCalendarKind.Gregorian)
        {
            var day = Math.Min(anniversary.Day, DateTime.DaysInMonth(year, anniversary.Month));
            return new DateOnly(year, anniversary.Month, day);
        }

        try
        {
            return _lunarCalendarService.ToGregorian(
                year,
                anniversary.Month,
                anniversary.Day,
                anniversary.IsLeapMonth);
        }
        catch (ArgumentOutOfRangeException exception)
            when (anniversary.IsLeapMonth && exception.ParamName == LunarCalendarService.LeapMonthParameterName)
        {
            return _lunarCalendarService.ToGregorian(year, anniversary.Month, anniversary.Day, false);
        }
    }
}
