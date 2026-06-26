namespace QingLi.Core.Reminders;

public interface INotificationService
{
    Task ShowBirthdayAsync(ReminderCandidate candidate, CancellationToken cancellationToken);
}
