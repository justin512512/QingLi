using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;

namespace QingLi.Core.Tests.Birthdays;

public sealed class BirthdayOccurrenceServiceTests
{
    [Fact]
    public void Gregorian_birthday_uses_same_month_and_day()
    {
        var birthday = new Birthday(Guid.NewGuid(), "\u5C0F\u6797",
            BirthdayCalendarKind.Gregorian, 1990, 8, 18, false, 3,
            new TimeOnly(9, 0), null, true);

        var actual = new BirthdayOccurrenceService().GetOccurrence(birthday, 2027);

        Assert.Equal(new DateOnly(2027, 8, 18), actual);
    }

    [Fact]
    public void Gregorian_february_29_falls_back_to_last_day_in_non_leap_year()
    {
        var birthday = new Birthday(Guid.NewGuid(), "\u5C0F\u5468",
            BirthdayCalendarKind.Gregorian, 2000, 2, 29, false, 0,
            new TimeOnly(8, 0), null, true);

        Assert.Equal(new DateOnly(2027, 2, 28),
            new BirthdayOccurrenceService().GetOccurrence(birthday, 2027));
    }

    [Fact]
    public void Lunar_leap_month_birthday_falls_back_to_regular_month_when_target_year_has_no_leap_month()
    {
        var birthday = new Birthday(Guid.NewGuid(), "\u5C0F\u590F",
            BirthdayCalendarKind.Lunar, 2028, 5, 1, true, 1,
            new TimeOnly(9, 0), null, true);

        var actual = new BirthdayOccurrenceService(new LunarCalendarService()).GetOccurrence(birthday, 2027);

        Assert.Equal(new DateOnly(2027, 6, 5), actual);
        Assert.Equal(new LunarDate(2027, 5, 1, false), new LunarCalendarService().FromGregorian(actual));
    }

    [Fact]
    public void Lunar_day_30_falls_back_to_last_day_of_small_month()
    {
        var birthday = new Birthday(Guid.NewGuid(), "\u5C0F\u79CB",
            BirthdayCalendarKind.Lunar, 2028, 5, 30, false, 2,
            new TimeOnly(8, 30), null, true);

        var actual = new BirthdayOccurrenceService(new LunarCalendarService()).GetOccurrence(birthday, 2027);

        Assert.Equal(new DateOnly(2027, 7, 3), actual);
        Assert.Equal(new LunarDate(2027, 5, 29, false), new LunarCalendarService().FromGregorian(actual));
    }
}
