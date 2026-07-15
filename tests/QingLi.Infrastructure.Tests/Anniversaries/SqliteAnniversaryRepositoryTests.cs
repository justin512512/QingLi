using Microsoft.Data.Sqlite;
using QingLi.Core.Anniversaries;
using QingLi.Infrastructure.Anniversaries;
using QingLi.Infrastructure.Data;
using QingLi.Infrastructure.Tests.Support;

namespace QingLi.Infrastructure.Tests.Anniversaries;

public sealed class SqliteAnniversaryRepositoryTests
{
    [Fact]
    public async Task SavesUpdatesAndReadsAnniversaryAcrossRepositoryInstances()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = Sample("结婚纪念日", 5, 20);
        var first = new SqliteAnniversaryRepository(database.Factory);
        await first.SaveAsync(original, default);
        await first.SaveAsync(original with { Title = "我们的结婚纪念日", Notes = "准备礼物" }, default);

        var reopened = new SqliteAnniversaryRepository(database.Factory);
        var actual = await reopened.GetAsync(original.Id, default);

        Assert.Equal("我们的结婚纪念日", actual?.Title);
        Assert.Equal("准备礼物", actual?.Notes);
    }

    [Fact]
    public async Task ListsByTitleAndOrdersByNextOccurrence()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteAnniversaryRepository(database.Factory);
        var later = Sample("家庭纪念日", 12, 1);
        var sooner = Sample("相识纪念日", 7, 1);
        var unrelated = Sample("入职日期", 1, 1);
        await repository.SaveAsync(later, default);
        await repository.SaveAsync(sooner, default);
        await repository.SaveAsync(unrelated, default);

        var actual = await repository.ListAsync("纪念", new DateOnly(2026, 6, 24), default);

        Assert.Equal([sooner.Id, later.Id], actual.Select(item => item.Id));
    }

    [Fact]
    public async Task DeleteRemovesOnlySelectedAnniversary()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteAnniversaryRepository(database.Factory);
        var first = Sample("甲", 1, 1);
        var second = Sample("乙", 2, 2);
        await repository.SaveAsync(first, default);
        await repository.SaveAsync(second, default);

        await repository.DeleteAsync(first.Id, default);

        Assert.Null(await repository.GetAsync(first.Id, default));
        Assert.NotNull(await repository.GetAsync(second.Id, default));
    }

    [Fact]
    public async Task MigrationAddsAnniversariesWithoutChangingExistingBirthdays()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QingLi.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "qingli.db");
        var factory = new SqliteConnectionFactory(path);

        try
        {
            await using (var connection = factory.Create())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE birthdays (
                      id TEXT PRIMARY KEY, name TEXT NOT NULL, calendar_kind INTEGER NOT NULL,
                      birth_year INTEGER NOT NULL, month INTEGER NOT NULL, day INTEGER NOT NULL,
                      is_leap_month INTEGER NOT NULL, reminder_days_before INTEGER NOT NULL,
                      reminder_time TEXT NOT NULL, notes TEXT NULL, is_enabled INTEGER NOT NULL);
                    INSERT INTO birthdays VALUES (
                      '11111111-1111-1111-1111-111111111111', '旧生日', 0, 1990, 1, 2, 0, 3,
                      '09:00:00.0000000', NULL, 1);
                    CREATE TABLE reminder_history (
                      birthday_id TEXT NOT NULL, scheduled_at TEXT NOT NULL,
                      occurrence_date TEXT NOT NULL, sent_at TEXT NOT NULL,
                      PRIMARY KEY (birthday_id, scheduled_at));
                    CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var result = await new DatabaseMigrator(factory).TryMigrateAsync(default);

            Assert.True(result.IsWritable, result.ErrorMessage);
            await using var verification = factory.Create();
            await verification.OpenAsync();
            await using var verifyCommand = verification.CreateCommand();
            verifyCommand.CommandText = "SELECT (SELECT count(*) FROM birthdays), (SELECT count(*) FROM anniversaries);";
            await using var reader = await verifyCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(0, reader.GetInt32(1));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static Anniversary Sample(string title, int month, int day) => new(
        Guid.NewGuid(),
        title,
        AnniversaryCalendarKind.Gregorian,
        2020,
        month,
        day,
        false,
        3,
        new TimeOnly(9, 0),
        null,
        true);
}
