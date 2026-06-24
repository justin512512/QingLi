namespace QingLi.Core.Birthdays;

public enum BirthdayCalendarKind
{
    Gregorian,
    Lunar
}

public sealed record Birthday(
    Guid Id,
    string Name,
    BirthdayCalendarKind CalendarKind,
    int BirthYear,
    int Month,
    int Day,
    bool IsLeapMonth,
    int ReminderDaysBefore,
    TimeOnly ReminderTime,
    string? Notes,
    bool IsEnabled);
