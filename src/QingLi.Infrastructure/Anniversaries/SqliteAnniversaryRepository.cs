using System.Globalization;
using Microsoft.Data.Sqlite;
using QingLi.Core.Anniversaries;
using QingLi.Infrastructure.Data;

namespace QingLi.Infrastructure.Anniversaries;

public sealed class SqliteAnniversaryRepository(SqliteConnectionFactory connectionFactory)
    : IAnniversaryRepository
{
    public async Task<IReadOnlyList<Anniversary>> ListAsync(
        string? titleFilter,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var anniversaries = new List<Anniversary>();
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, calendar_kind, start_year, month, day, is_leap_month,
                   reminder_days_before, reminder_time, notes, is_enabled
            FROM anniversaries
            WHERE $title = '' OR instr(title, $title) > 0;
            """;
        command.Parameters.AddWithValue("$title", titleFilter?.Trim() ?? string.Empty);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            anniversaries.Add(ReadAnniversary(reader));
        }

        var occurrences = new AnniversaryOccurrenceService();
        return anniversaries
            .OrderBy(item => NextOccurrence(item, today, occurrences))
            .ThenBy(item => item.Title, StringComparer.CurrentCulture)
            .ToArray();
    }

    public async Task<Anniversary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, calendar_kind, start_year, month, day, is_leap_month,
                   reminder_days_before, reminder_time, notes, is_enabled
            FROM anniversaries WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAnniversary(reader) : null;
    }

    public async Task SaveAsync(Anniversary anniversary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anniversary);
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO anniversaries (
              id, title, calendar_kind, start_year, month, day, is_leap_month,
              reminder_days_before, reminder_time, notes, is_enabled)
            VALUES (
              $id, $title, $calendarKind, $startYear, $month, $day, $isLeapMonth,
              $reminderDaysBefore, $reminderTime, $notes, $isEnabled)
            ON CONFLICT(id) DO UPDATE SET
              title = excluded.title,
              calendar_kind = excluded.calendar_kind,
              start_year = excluded.start_year,
              month = excluded.month,
              day = excluded.day,
              is_leap_month = excluded.is_leap_month,
              reminder_days_before = excluded.reminder_days_before,
              reminder_time = excluded.reminder_time,
              notes = excluded.notes,
              is_enabled = excluded.is_enabled;
            """;
        AddParameters(command, anniversary);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM anniversaries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateOnly NextOccurrence(
        Anniversary anniversary,
        DateOnly today,
        AnniversaryOccurrenceService occurrences)
    {
        var occurrence = occurrences.GetOccurrence(anniversary, today.Year);
        return occurrence >= today ? occurrence : occurrences.GetOccurrence(anniversary, today.Year + 1);
    }

    private static Anniversary ReadAnniversary(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        (AnniversaryCalendarKind)reader.GetInt32(2),
        reader.GetInt32(3),
        reader.GetInt32(4),
        reader.GetInt32(5),
        reader.GetBoolean(6),
        reader.GetInt32(7),
        TimeOnly.ParseExact(reader.GetString(8), "HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.GetBoolean(10));

    private static void AddParameters(SqliteCommand command, Anniversary anniversary)
    {
        command.Parameters.AddWithValue("$id", anniversary.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", anniversary.Title);
        command.Parameters.AddWithValue("$calendarKind", (int)anniversary.CalendarKind);
        command.Parameters.AddWithValue("$startYear", anniversary.StartYear);
        command.Parameters.AddWithValue("$month", anniversary.Month);
        command.Parameters.AddWithValue("$day", anniversary.Day);
        command.Parameters.AddWithValue("$isLeapMonth", anniversary.IsLeapMonth);
        command.Parameters.AddWithValue("$reminderDaysBefore", anniversary.ReminderDaysBefore);
        command.Parameters.AddWithValue(
            "$reminderTime",
            anniversary.ReminderTime.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$notes", (object?)anniversary.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$isEnabled", anniversary.IsEnabled);
    }
}
