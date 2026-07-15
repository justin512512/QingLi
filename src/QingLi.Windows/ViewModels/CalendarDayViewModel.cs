using System.Collections.ObjectModel;
using QingLi.Core.Calendars;

namespace QingLi.Windows.ViewModels;

public sealed class CalendarDayViewModel
{
    private static readonly string[] LunarMonths =
        ["正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月"];

    private static readonly string[] LunarDays =
    [
        "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
        "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
        "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
    ];

    private static readonly string[] Weekdays =
        ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];

    public CalendarDayViewModel(CalendarDay day, DateOnly? today = null)
    {
        Date = day.Date;
        LunarDayText = FormatLunarDay(day.Lunar.Day);
        LunarText = $"{FormatLunarMonth(day.Lunar)}{LunarDayText}";
        SolarTerm = day.SolarTerm;
        HasSolarTerm = !string.IsNullOrWhiteSpace(SolarTerm);
        HolidayName = day.Holiday?.Name;
        IsRestDay = day.Holiday is { IsWorkday: false };
        IsMakeupWorkday = day.Holiday is { IsWorkday: true };
        IsCurrentMonth = day.IsCurrentMonth;
        IsToday = Date == today;
        IsWeekend = Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        WeekdayText = Weekdays[(int)Date.DayOfWeek];
        SecondaryText = SolarTerm ?? HolidayName ?? LunarDayText;
    }

    public DateOnly Date { get; }

    public string LunarText { get; }

    public string LunarDayText { get; }

    public string WeekdayText { get; }

    public string SecondaryText { get; }

    public string? SolarTerm { get; }

    public bool HasSolarTerm { get; }

    public string? HolidayName { get; }

    public bool IsRestDay { get; }

    public bool IsMakeupWorkday { get; }

    public bool IsCurrentMonth { get; }

    public bool IsToday { get; }

    public bool IsWeekend { get; }

    public ObservableCollection<string> Birthdays { get; } = [];

    private static string FormatLunarMonth(LunarDate lunar)
    {
        var month = lunar.Month is >= 1 and <= 12 ? LunarMonths[lunar.Month - 1] : $"{lunar.Month}月";
        return lunar.IsLeapMonth ? $"闰{month}" : month;
    }

    private static string FormatLunarDay(int day) =>
        day is >= 1 and <= 30 ? LunarDays[day - 1] : day.ToString();
}
