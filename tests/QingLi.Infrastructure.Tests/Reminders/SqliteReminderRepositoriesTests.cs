using QingLi.Core.Reminders;
using QingLi.Infrastructure.Data;
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
            ReminderSubjectKind.Birthday,
            Guid.NewGuid(),
            "小林",
            new DateOnly(2027, 8, 18),
            scheduled);

        await repository.RecordSentAsync(candidate, scheduled.AddMinutes(1), default);
        await repository.RecordSentAsync(candidate, scheduled.AddMinutes(2), default);

        Assert.True(await repository.WasSentAsync(
            candidate.SubjectKind,
            candidate.SubjectId,
            candidate.OccurrenceDate,
            default));
    }

    [Fact]
    public async Task HistorySeparatesBirthdayAndAnniversaryWithSameId()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteReminderHistoryRepository(database.Factory);
        var id = Guid.NewGuid();
        var occurrence = new DateOnly(2027, 8, 18);
        var scheduled = new DateTimeOffset(2027, 8, 15, 9, 0, 0, TimeSpan.FromHours(8));
        var birthday = new ReminderCandidate(
            ReminderSubjectKind.Birthday, id, "小林", occurrence, scheduled);

        await repository.RecordSentAsync(birthday, scheduled.AddMinutes(1), default);

        Assert.True(await repository.WasSentAsync(
            ReminderSubjectKind.Birthday, id, occurrence, default));
        Assert.False(await repository.WasSentAsync(
            ReminderSubjectKind.Anniversary, id, occurrence, default));
    }

    [Fact]
    public async Task MigrationPreservesLegacyBirthdayReminderHistory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QingLi.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var factory = new SqliteConnectionFactory(Path.Combine(directory, "qingli.db"));
        var birthdayId = Guid.NewGuid();
        var occurrence = new DateOnly(2027, 8, 18);

        try
        {
            await using (var connection = factory.Create())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE reminder_history (
                      birthday_id TEXT NOT NULL,
                      scheduled_at TEXT NOT NULL,
                      occurrence_date TEXT NOT NULL,
                      sent_at TEXT NOT NULL,
                      PRIMARY KEY (birthday_id, scheduled_at));
                    INSERT INTO reminder_history VALUES ($id, $scheduled, '2027-08-18', $sent);
                    """;
                command.Parameters.AddWithValue("$id", birthdayId.ToString("D"));
                command.Parameters.AddWithValue("$scheduled", "2027-08-15T09:00:00.0000000+08:00");
                command.Parameters.AddWithValue("$sent", "2027-08-15T09:01:00.0000000+08:00");
                await command.ExecuteNonQueryAsync();
            }

            var migration = await new DatabaseMigrator(factory).TryMigrateAsync(default);
            var repository = new SqliteReminderHistoryRepository(factory);

            Assert.True(migration.IsWritable, migration.ErrorMessage);
            Assert.True(await repository.WasSentAsync(
                ReminderSubjectKind.Birthday,
                birthdayId,
                occurrence,
                default));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
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
