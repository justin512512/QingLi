using QingLi.Core.Holidays;

namespace QingLi.Core.Calendars;

public sealed record CalendarDay(
    DateOnly Date,
    LunarDate Lunar,
    string? SolarTerm,
    HolidayDefinition? Holiday,
    bool IsCurrentMonth);
