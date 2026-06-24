using QingLi.Core.Birthdays;
using QingLi.Core.Reminders;
using QingLi.Windows.Scheduling;

namespace QingLi.Windows.Tests.Scheduling;

public sealed class ReminderSchedulerTests
{
    [Fact]
    public async Task Wake_check_sends_due_reminder_once()
    {
        var fixture = new SchedulerFixture();
        var now = new DateTimeOffset(2027, 8, 15, 10, 0, 0, TimeSpan.FromHours(8));

        await fixture.Scheduler.CheckAsync(now, default);
        await fixture.Scheduler.CheckAsync(now, default);

        Assert.Single(fixture.Notifications.Sent);
    }

    [Fact]
    public async Task Suppress_today_skips_only_candidates_scheduled_today()
    {
        var fixture = new SchedulerFixture();
        await fixture.Suppression.SuppressAsync(new DateOnly(2027, 8, 15), default);

        await fixture.Scheduler.CheckAsync(
            new DateTimeOffset(2027, 8, 15, 10, 0, 0, TimeSpan.FromHours(8)), default);

        Assert.Empty(fixture.Notifications.Sent);
        Assert.True(fixture.Birthday.IsEnabled);
    }

    [Fact]
    public async Task Wake_check_does_not_send_stale_previous_day_reminder()
    {
        var fixture = new SchedulerFixture(
            lastCheck: new DateTimeOffset(2027, 8, 14, 8, 0, 0, TimeSpan.FromHours(8)));

        await fixture.Scheduler.CheckAsync(
            new DateTimeOffset(2027, 8, 16, 10, 0, 0, TimeSpan.FromHours(8)), default);

        Assert.Empty(fixture.Notifications.Sent);
    }

    private sealed class SchedulerFixture
    {
        public SchedulerFixture(DateTimeOffset? lastCheck = null)
        {
            Birthday = new Birthday(
                Guid.NewGuid(), "小林", BirthdayCalendarKind.Gregorian,
                1990, 8, 18, false, 3, new TimeOnly(9, 0), null, true);
            var repository = new BirthdayRepository(Birthday);
            Notifications = new NotificationSink();
            Suppression = new Suppression();
            Scheduler = new ReminderScheduler(
                repository,
                new ReminderPlanner(new BirthdayOccurrenceService()),
                new History(),
                Suppression,
                Notifications,
                lastCheck ?? new DateTimeOffset(2027, 8, 15, 8, 0, 0, TimeSpan.FromHours(8)));
        }

        public Birthday Birthday { get; }
        public NotificationSink Notifications { get; }
        public Suppression Suppression { get; }
        public ReminderScheduler Scheduler { get; }
    }

    private sealed class BirthdayRepository(Birthday birthday) : IBirthdayRepository
    {
        public Task<IReadOnlyList<Birthday>> ListAsync(
            string? nameFilter, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Birthday>>([birthday]);

        public Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Birthday?>(birthday.Id == id ? birthday : null);

        public Task SaveAsync(Birthday value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class History : IReminderHistoryRepository
    {
        private readonly HashSet<(Guid Id, DateTimeOffset At)> _sent = [];

        public Task<bool> WasSentAsync(
            Guid birthdayId, DateTimeOffset scheduledAt, CancellationToken cancellationToken) =>
            Task.FromResult(_sent.Contains((birthdayId, scheduledAt)));

        public Task RecordSentAsync(
            ReminderCandidate candidate, DateTimeOffset sentAt, CancellationToken cancellationToken)
        {
            _sent.Add((candidate.BirthdayId, candidate.ScheduledAt));
            return Task.CompletedTask;
        }
    }

    public sealed class Suppression : IReminderSuppression
    {
        private DateOnly? _date;

        public Task<DateOnly?> GetSuppressedDateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_date);

        public Task SuppressAsync(DateOnly localDate, CancellationToken cancellationToken)
        {
            _date = localDate;
            return Task.CompletedTask;
        }
    }

    public sealed class NotificationSink : IReminderNotificationSink
    {
        public List<ReminderCandidate> Sent { get; } = [];

        public Task SendAsync(ReminderCandidate candidate, CancellationToken cancellationToken)
        {
            Sent.Add(candidate);
            return Task.CompletedTask;
        }
    }
}
