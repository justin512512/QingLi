using QingLi.Core.Calendars;

namespace QingLi.Core.Birthdays;

public sealed class BirthdayOccurrenceService
{
    private readonly LunarCalendarService _lunarCalendarService;

    public BirthdayOccurrenceService(LunarCalendarService? lunarCalendarService = null)
    {
        _lunarCalendarService = lunarCalendarService ?? new LunarCalendarService();
    }

    public DateOnly GetOccurrence(Birthday birthday, int year)
    {
        if (birthday.CalendarKind == BirthdayCalendarKind.Gregorian)
        {
            var day = Math.Min(birthday.Day, DateTime.DaysInMonth(year, birthday.Month));
            return new DateOnly(year, birthday.Month, day);
        }

        try
        {
            return _lunarCalendarService.ToGregorian(year, birthday.Month, birthday.Day, birthday.IsLeapMonth);
        }
        catch (ArgumentOutOfRangeException exception)
            when (birthday.IsLeapMonth && exception.ParamName == LunarCalendarService.LeapMonthParameterName)
        {
            return _lunarCalendarService.ToGregorian(year, birthday.Month, birthday.Day, false);
        }
    }
}
