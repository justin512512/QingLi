using QingLi.Core.Almanac;

namespace QingLi.Windows.ViewModels;

public sealed class AlmanacSummaryViewModel(AlmanacDay value)
{
    private static readonly string[] Weekdays =
        ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];

    public DateOnly Date { get; } = value.Date;
    public int DayNumber { get; } = value.Date.Day;
    public string WeekdayText { get; } = Weekdays[(int)value.Date.DayOfWeek];
    public string LunarMonthDay { get; } = value.LunarMonthDay;
    public string GanZhiText { get; } = $"{value.YearGanZhi}年 · {value.MonthGanZhi}月 · {value.DayGanZhi}日";
    public string ZodiacText { get; } = $"生肖{value.Zodiac}";
    public string? SolarTerm { get; } = value.SolarTerm;
    public IReadOnlyList<string> Festivals { get; } = value.Festivals;
    public IReadOnlyList<string> Suitable { get; } = value.Suitable;
    public IReadOnlyList<string> Avoid { get; } = value.Avoid;
}
