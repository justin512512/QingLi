namespace QingLi.Core.Upcoming;

public interface IUpcomingEventService
{
    Task<IReadOnlyList<UpcomingEvent>> GetUpcomingAsync(
        DateOnly today,
        int horizonDays,
        CancellationToken cancellationToken);
}
