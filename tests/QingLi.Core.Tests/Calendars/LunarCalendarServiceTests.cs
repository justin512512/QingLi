using QingLi.Core.Calendars;

namespace QingLi.Core.Tests.Calendars;

public sealed class LunarCalendarServiceTests
{
    private readonly LunarCalendarService _service = new();

    [Theory]
    [InlineData(2026, 2, 17, 2026, 1, 1, false)]
    [InlineData(2026, 9, 25, 2026, 8, 15, false)]
    [InlineData(2028, 6, 22, 2028, 5, 30, false)]
    [InlineData(2028, 6, 23, 2028, 5, 1, true)]
    [InlineData(2028, 7, 22, 2028, 6, 1, false)]
    public void Converts_known_dates_both_directions(
        int gregorianYear,
        int gregorianMonth,
        int gregorianDay,
        int lunarYear,
        int lunarMonth,
        int lunarDay,
        bool isLeapMonth)
    {
        var gregorian = new DateOnly(gregorianYear, gregorianMonth, gregorianDay);

        var lunar = _service.FromGregorian(gregorian);

        Assert.Equal(new LunarDate(lunarYear, lunarMonth, lunarDay, isLeapMonth), lunar);
        Assert.Equal(gregorian, _service.ToGregorian(lunarYear, lunarMonth, lunarDay, isLeapMonth));
    }
}
