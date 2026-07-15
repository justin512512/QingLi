using Lunar;

namespace QingLi.Core.Almanac;

public sealed class LunarSharpAlmanacService : IAlmanacService
{
    public AlmanacDay GetDay(DateOnly date)
    {
        var lunar = new Solar(date.Year, date.Month, date.Day, 0, 0, 0).Lunar;
        var festivals = lunar.Festivals
            .Concat(lunar.OtherFestivals)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new AlmanacDay(
            date,
            $"{lunar.MonthInChinese}月{lunar.DayInChinese}",
            lunar.YearInGanZhi,
            lunar.MonthInGanZhi,
            lunar.DayInGanZhi,
            lunar.YearShengXiao,
            string.IsNullOrWhiteSpace(lunar.JieQi) ? null : lunar.JieQi,
            festivals,
            lunar.DayYi.ToArray(),
            lunar.DayJi.ToArray());
    }
}
