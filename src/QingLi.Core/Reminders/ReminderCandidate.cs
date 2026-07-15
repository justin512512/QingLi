namespace QingLi.Core.Reminders;

public enum ReminderSubjectKind
{
    Birthday,
    Anniversary
}

public sealed record ReminderCandidate(
    ReminderSubjectKind SubjectKind,
    Guid SubjectId,
    string Name,
    DateOnly OccurrenceDate,
    DateTimeOffset ScheduledAt);
