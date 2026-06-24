namespace QingLi.Core.Reminders;

public sealed record ReminderCandidate(
    Guid BirthdayId,
    string Name,
    DateOnly OccurrenceDate,
    DateTimeOffset ScheduledAt);
