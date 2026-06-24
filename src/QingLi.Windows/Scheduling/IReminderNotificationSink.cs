using QingLi.Core.Reminders;

namespace QingLi.Windows.Scheduling;

public interface IReminderNotificationSink
{
    Task SendAsync(ReminderCandidate candidate, CancellationToken cancellationToken);
}
