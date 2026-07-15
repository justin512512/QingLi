namespace QingLi.Core.Almanac;

public sealed record AlmanacDay(
    DateOnly Date,
    string LunarMonthDay,
    string YearGanZhi,
    string MonthGanZhi,
    string DayGanZhi,
    string Zodiac,
    string? SolarTerm,
    IReadOnlyList<string> Festivals,
    IReadOnlyList<string> Suitable,
    IReadOnlyList<string> Avoid);
