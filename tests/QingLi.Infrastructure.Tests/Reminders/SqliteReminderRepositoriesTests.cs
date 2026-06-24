using QingLi.Core.Reminders;
using QingLi.Infrastructure.Reminders;
using QingLi.Infrastructure.Tests.Support;

namespace QingLi.Infrastructure.Tests.Reminders;

public sealed class SqliteReminderRepositoriesTests
{
    [Fact]
    public async Task History_round_trip_deduplicates_same_schedule()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteReminderHistoryRepository(database.Factory);
        var scheduled = new DateTimeOffset(2027, 8, 15, 9, 0, 0, TimeSpan.FromHours(8));
        var candidate = new ReminderCandidate(
            Guid.NewGuid(), "小林", new DateOnly(2027, 8, 18), scheduled);

        await repository.RecordSentAsync(candidate, scheduled.AddMinutes(1), default);
        await repository.RecordSentAsync(candidate, scheduled.AddMinutes(2), default);

        Assert.True(await repository.WasSentAsync(candidate.BirthdayId, scheduled, default));
    }

    [Fact]
    public async Task Suppression_round_trip_keeps_only_configured_local_date()
    {
        await using var database = await TestDatabase.CreateAsync();
        var suppression = new SqliteReminderSuppression(database.Factory);

        await suppression.SuppressAsync(new DateOnly(2027, 8, 15), default);

        Assert.Equal(
            new DateOnly(2027, 8, 15),
            await suppression.GetSuppressedDateAsync(default));
    }
}
