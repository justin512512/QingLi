using Microsoft.Data.Sqlite;

namespace QingLi.Infrastructure.Data;

public sealed record DatabaseMigrationResult(
    bool IsWritable,
    string? PreservedCopyPath = null,
    string? ErrorMessage = null);

public sealed class DatabaseMigrator(SqliteConnectionFactory connectionFactory)
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS birthdays (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          calendar_kind INTEGER NOT NULL,
          birth_year INTEGER NOT NULL,
          month INTEGER NOT NULL,
          day INTEGER NOT NULL,
          is_leap_month INTEGER NOT NULL,
          reminder_days_before INTEGER NOT NULL,
          reminder_time TEXT NOT NULL,
          notes TEXT NULL,
          is_enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS reminder_history (
          birthday_id TEXT NOT NULL,
          scheduled_at TEXT NOT NULL,
          occurrence_date TEXT NOT NULL,
          sent_at TEXT NOT NULL,
          PRIMARY KEY (birthday_id, scheduled_at)
        );

        CREATE TABLE IF NOT EXISTS settings (
          key TEXT PRIMARY KEY,
          value TEXT NOT NULL
        );
        """;

    public async Task<DatabaseMigrationResult> TryMigrateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(connectionFactory.DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = Schema;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new DatabaseMigrationResult(true);
        }
        catch (SqliteException exception)
        {
            var preservedCopy = await PreserveCorruptDatabaseAsync(cancellationToken);
            await TryOpenReadOnlyAsync(cancellationToken);
            return new DatabaseMigrationResult(false, preservedCopy, exception.Message);
        }
    }

    private async Task<string?> PreserveCorruptDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(connectionFactory.DatabasePath))
        {
            return null;
        }

        var copyPath = $"{connectionFactory.DatabasePath}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.corrupt-copy";
        await using var source = new FileStream(
            connectionFactory.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using var target = new FileStream(
            copyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
        return copyPath;
    }

    private async Task TryOpenReadOnlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = connectionFactory.Create(readOnly: true);
            await connection.OpenAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // The original file and its preserved copy remain untouched for manual recovery.
        }
    }
}
