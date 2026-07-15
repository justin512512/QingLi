namespace QingLi.Core.Upcoming;

public sealed record UpcomingEvent(
    DateOnly Date,
    int DaysRemaining,
    UpcomingEventKind Kind,
    string Title,
    bool? IsRestDay = null,
    Guid? SubjectId = null);
