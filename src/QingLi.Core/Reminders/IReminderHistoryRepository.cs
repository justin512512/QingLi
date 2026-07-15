namespace QingLi.Core.Reminders;

public interface IReminderHistoryRepository
{
    Task<bool> WasSentAsync(
        ReminderSubjectKind subjectKind,
        Guid subjectId,
        DateOnly occurrenceDate,
        CancellationToken cancellationToken);

    Task RecordSentAsync(
        ReminderCandidate candidate,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}
