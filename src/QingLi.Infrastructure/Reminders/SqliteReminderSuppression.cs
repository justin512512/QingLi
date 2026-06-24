using System.Globalization;
using Microsoft.Data.Sqlite;
using QingLi.Core.Reminders;
using QingLi.Infrastructure.Data;

namespace QingLi.Infrastructure.Reminders;

public sealed class SqliteReminderSuppression(SqliteConnectionFactory connectionFactory)
    : IReminderSuppression
{
    private const string SettingKey = "reminders.suppressed-local-date";

    public async Task<DateOnly?> GetSuppressedDateAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", SettingKey);
        var value = await command.ExecuteScalarAsync(cancellationToken) as string;
        return DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    public async Task SuppressAsync(DateOnly localDate, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", SettingKey);
        command.Parameters.AddWithValue(
            "$value", localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
