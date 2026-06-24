using System.Globalization;

namespace QingLi.Core.Calendars;

public sealed class LunarCalendarService
{
    private readonly ChineseLunisolarCalendar _calendar = new();

    public LunarDate FromGregorian(DateOnly date)
    {
        var value = date.ToDateTime(TimeOnly.MinValue);
        var year = _calendar.GetYear(value);
        var rawMonth = _calendar.GetMonth(value);
        var leapMonth = _calendar.GetLeapMonth(year);
        var isLeapMonth = leapMonth > 0 && rawMonth == leapMonth;
        var month = leapMonth > 0 && rawMonth >= leapMonth
            ? rawMonth - 1
            : rawMonth;

        return new LunarDate(year, month, _calendar.GetDayOfMonth(value), isLeapMonth);
    }

    public DateOnly ToGregorian(int year, int month, int day, bool isLeapMonth)
    {
        var leapMonth = _calendar.GetLeapMonth(year);
        var rawMonth = month;

        if (isLeapMonth)
        {
            if (leapMonth <= 0 || leapMonth != month + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(isLeapMonth));
            }

            rawMonth = leapMonth;
        }
        else if (leapMonth > 0 && month >= leapMonth)
        {
            rawMonth = month + 1;
        }

        var normalizedDay = Math.Min(day, _calendar.GetDaysInMonth(year, rawMonth));
        var value = _calendar.ToDateTime(year, rawMonth, normalizedDay, 0, 0, 0, 0);
        return DateOnly.FromDateTime(value);
    }
}
