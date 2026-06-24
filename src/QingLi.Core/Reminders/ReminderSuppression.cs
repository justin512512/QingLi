namespace QingLi.Core.Reminders;

public interface IReminderSuppression
{
    Task<DateOnly?> GetSuppressedDateAsync(CancellationToken cancellationToken);

    Task SuppressAsync(DateOnly localDate, CancellationToken cancellationToken);
}
