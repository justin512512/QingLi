using System.Globalization;
using Microsoft.Data.Sqlite;
using QingLi.Core.Birthdays;
using QingLi.Infrastructure.Data;

namespace QingLi.Infrastructure.Birthdays;

public sealed class SqliteBirthdayRepository(SqliteConnectionFactory connectionFactory)
    : IBirthdayRepository
{
    public async Task<IReadOnlyList<Birthday>> ListAsync(
        string? nameFilter,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var birthdays = new List<Birthday>();
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, calendar_kind, birth_year, month, day, is_leap_month,
                   reminder_days_before, reminder_time, notes, is_enabled
            FROM birthdays
            WHERE $name = '' OR instr(name, $name) > 0;
            """;
        command.Parameters.AddWithValue("$name", nameFilter?.Trim() ?? string.Empty);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            birthdays.Add(ReadBirthday(reader));
        }

        var occurrences = new BirthdayOccurrenceService();
        return birthdays
            .OrderBy(birthday => NextOccurrence(birthday, today, occurrences))
            .ThenBy(birthday => birthday.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    public async Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, calendar_kind, birth_year, month, day, is_leap_month,
                   reminder_days_before, reminder_time, notes, is_enabled
            FROM birthdays WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBirthday(reader) : null;
    }

    public async Task SaveAsync(Birthday birthday, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(birthday);
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO birthdays (
              id, name, calendar_kind, birth_year, month, day, is_leap_month,
              reminder_days_before, reminder_time, notes, is_enabled)
            VALUES (
              $id, $name, $calendarKind, $birthYear, $month, $day, $isLeapMonth,
              $reminderDaysBefore, $reminderTime, $notes, $isEnabled)
            ON CONFLICT(id) DO UPDATE SET
              name = excluded.name,
              calendar_kind = excluded.calendar_kind,
              birth_year = excluded.birth_year,
              month = excluded.month,
              day = excluded.day,
              is_leap_month = excluded.is_leap_month,
              reminder_days_before = excluded.reminder_days_before,
              reminder_time = excluded.reminder_time,
              notes = excluded.notes,
              is_enabled = excluded.is_enabled;
            """;
        AddBirthdayParameters(command, birthday);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM birthdays WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateOnly NextOccurrence(
        Birthday birthday,
        DateOnly today,
        BirthdayOccurrenceService occurrences)
    {
        var occurrence = occurrences.GetOccurrence(birthday, today.Year);
        return occurrence >= today ? occurrence : occurrences.GetOccurrence(birthday, today.Year + 1);
    }

    private static Birthday ReadBirthday(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            (BirthdayCalendarKind)reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetBoolean(6),
            reader.GetInt32(7),
            TimeOnly.ParseExact(reader.GetString(8), "HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetBoolean(10));

    private static void AddBirthdayParameters(SqliteCommand command, Birthday birthday)
    {
        command.Parameters.AddWithValue("$id", birthday.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", birthday.Name);
        command.Parameters.AddWithValue("$calendarKind", (int)birthday.CalendarKind);
        command.Parameters.AddWithValue("$birthYear", birthday.BirthYear);
        command.Parameters.AddWithValue("$month", birthday.Month);
        command.Parameters.AddWithValue("$day", birthday.Day);
        command.Parameters.AddWithValue("$isLeapMonth", birthday.IsLeapMonth);
        command.Parameters.AddWithValue("$reminderDaysBefore", birthday.ReminderDaysBefore);
        command.Parameters.AddWithValue(
            "$reminderTime",
            birthday.ReminderTime.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$notes", (object?)birthday.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$isEnabled", birthday.IsEnabled);
    }
}
