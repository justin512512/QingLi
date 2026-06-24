namespace QingLi.Core.Birthdays;

public sealed class BirthdayOccurrenceService
{
    public DateOnly GetOccurrence(Birthday birthday, int year)
    {
        if (birthday.CalendarKind != BirthdayCalendarKind.Gregorian)
        {
            throw new NotSupportedException("Lunar birthdays are implemented separately.");
        }

        var day = Math.Min(birthday.Day, DateTime.DaysInMonth(year, birthday.Month));
        return new DateOnly(year, birthday.Month, day);
    }
}
