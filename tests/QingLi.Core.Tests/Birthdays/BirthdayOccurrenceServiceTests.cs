using QingLi.Core.Birthdays;

namespace QingLi.Core.Tests.Birthdays;

public sealed class BirthdayOccurrenceServiceTests
{
    [Fact]
    public void Gregorian_birthday_uses_same_month_and_day()
    {
        var birthday = new Birthday(Guid.NewGuid(), "小林",
            BirthdayCalendarKind.Gregorian, 1990, 8, 18, false, 3,
            new TimeOnly(9, 0), null, true);

        var actual = new BirthdayOccurrenceService().GetOccurrence(birthday, 2027);

        Assert.Equal(new DateOnly(2027, 8, 18), actual);
    }

    [Fact]
    public void Gregorian_february_29_falls_back_to_last_day_in_non_leap_year()
    {
        var birthday = new Birthday(Guid.NewGuid(), "小周",
            BirthdayCalendarKind.Gregorian, 2000, 2, 29, false, 0,
            new TimeOnly(8, 0), null, true);

        Assert.Equal(new DateOnly(2027, 2, 28),
            new BirthdayOccurrenceService().GetOccurrence(birthday, 2027));
    }
}
