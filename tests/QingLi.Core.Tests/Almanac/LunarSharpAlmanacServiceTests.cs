using QingLi.Core.Almanac;

namespace QingLi.Core.Tests.Almanac;

public sealed class LunarSharpAlmanacServiceTests
{
    [Fact]
    public void GetDay_ReturnsChineseAlmanacForFixedDate()
    {
        IAlmanacService service = new LunarSharpAlmanacService();

        var actual = service.GetDay(new DateOnly(2026, 7, 15));

        Assert.Equal(new DateOnly(2026, 7, 15), actual.Date);
        Assert.Equal("六月初二", actual.LunarMonthDay);
        Assert.Equal("丙午", actual.YearGanZhi);
        Assert.Equal("乙未", actual.MonthGanZhi);
        Assert.Equal("庚寅", actual.DayGanZhi);
        Assert.Equal("马", actual.Zodiac);
        Assert.Contains("开市", actual.Suitable);
        Assert.Contains("入宅", actual.Avoid);
    }

    [Fact]
    public void GetDay_AlwaysReturnsMaterializedCollections()
    {
        var actual = new LunarSharpAlmanacService().GetDay(new DateOnly(2026, 7, 15));

        Assert.NotNull(actual.Festivals);
        Assert.NotNull(actual.Suitable);
        Assert.NotNull(actual.Avoid);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(actual.Festivals);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(actual.Suitable);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(actual.Avoid);
    }
}
