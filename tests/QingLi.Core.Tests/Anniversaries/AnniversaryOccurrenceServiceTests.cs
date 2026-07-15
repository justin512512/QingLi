using QingLi.Core.Anniversaries;
using QingLi.Core.Calendars;

namespace QingLi.Core.Tests.Anniversaries;

public sealed class AnniversaryOccurrenceServiceTests
{
    [Fact]
    public void GregorianAnniversaryUsesSameMonthAndDay()
    {
        var anniversary = Sample(AnniversaryCalendarKind.Gregorian, 8, 18);

        var actual = new AnniversaryOccurrenceService().GetOccurrence(anniversary, 2027);

        Assert.Equal(new DateOnly(2027, 8, 18), actual);
    }

    [Fact]
    public void GregorianFebruary29UsesLastDayInNonLeapYear()
    {
        var anniversary = Sample(AnniversaryCalendarKind.Gregorian, 2, 29);

        var actual = new AnniversaryOccurrenceService().GetOccurrence(anniversary, 2027);

        Assert.Equal(new DateOnly(2027, 2, 28), actual);
    }

    [Fact]
    public void LunarLeapMonthFallsBackToRegularMonthWhenMissing()
    {
        var anniversary = Sample(AnniversaryCalendarKind.Lunar, 5, 1, isLeapMonth: true);

        var actual = new AnniversaryOccurrenceService(new LunarCalendarService())
            .GetOccurrence(anniversary, 2027);

        Assert.Equal(new DateOnly(2027, 6, 5), actual);
    }

    private static Anniversary Sample(
        AnniversaryCalendarKind calendarKind,
        int month,
        int day,
        bool isLeapMonth = false) => new(
            Guid.NewGuid(),
            "相识纪念日",
            calendarKind,
            2020,
            month,
            day,
            isLeapMonth,
            3,
            new TimeOnly(9, 0),
            null,
            true);
}
