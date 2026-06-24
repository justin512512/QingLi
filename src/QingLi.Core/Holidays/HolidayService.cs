namespace QingLi.Core.Holidays;

public sealed class HolidayService(IEnumerable<HolidayDefinition>? definitions = null)
{
    private readonly IReadOnlyDictionary<DateOnly, HolidayDefinition> _definitions = (definitions ?? [])
        .ToDictionary(value => value.Date);

    public HolidayDefinition? Find(DateOnly date)
    {
        return _definitions.TryGetValue(date, out var holiday)
            ? holiday
            : null;
    }
}
