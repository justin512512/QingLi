using QingLi.Core.Calendars;
using QingLi.Core.Holidays;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class CalendarDayViewModelTests
{
    [Fact]
    public void Formats_chinese_lunar_date_and_weekday()
    {
        var vm = new CalendarDayViewModel(new CalendarDay(
            new DateOnly(2026, 6, 30),
            new LunarDate(2026, 5, 16, false),
            null,
            null,
            true));

        Assert.Equal("五月十六", vm.LunarText);
        Assert.Equal("星期二", vm.WeekdayText);
        Assert.Equal("十六", vm.SecondaryText);
        Assert.False(vm.IsWeekend);
    }

    [Fact]
    public void Solar_term_has_priority_in_cell_subtitle()
    {
        var vm = new CalendarDayViewModel(new CalendarDay(
            new DateOnly(2026, 6, 21),
            new LunarDate(2026, 5, 7, false),
            "夏至",
            null,
            true));

        Assert.Equal("夏至", vm.SecondaryText);
        Assert.True(vm.IsWeekend);
    }

    [Fact]
    public void Marks_today()
    {
        var date = new DateOnly(2026, 6, 30);
        var vm = new CalendarDayViewModel(
            new CalendarDay(date, new LunarDate(2026, 5, 16, false), null, null, true),
            date);

        Assert.True(vm.IsToday);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void ExposesRestAndMakeupWorkdayBadges(
        bool isWorkday,
        bool expectedRest,
        bool expectedMakeupWorkday)
    {
        var vm = new CalendarDayViewModel(new CalendarDay(
            new DateOnly(2026, 10, 1),
            new LunarDate(2026, 8, 21, false),
            null,
            new HolidayDefinition(new DateOnly(2026, 10, 1), "国庆节", isWorkday),
            true));

        Assert.Equal(expectedRest, vm.IsRestDay);
        Assert.Equal(expectedMakeupWorkday, vm.IsMakeupWorkday);
    }
}
