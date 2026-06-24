namespace QingLi.Core.Reminders;

public interface IReminderHistoryRepository
{
    Task<bool> WasSentAsync(
        Guid birthdayId,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken);

    Task RecordSentAsync(
        ReminderCandidate candidate,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}
