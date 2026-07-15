namespace QingLi.Core.Anniversaries;

public enum AnniversaryCalendarKind
{
    Gregorian,
    Lunar
}

public sealed record Anniversary(
    Guid Id,
    string Title,
    AnniversaryCalendarKind CalendarKind,
    int StartYear,
    int Month,
    int Day,
    bool IsLeapMonth,
    int ReminderDaysBefore,
    TimeOnly ReminderTime,
    string? Notes,
    bool IsEnabled);
