using System.Globalization;
using Microsoft.Data.Sqlite;
using QingLi.Core.Reminders;
using QingLi.Infrastructure.Data;

namespace QingLi.Infrastructure.Reminders;

public sealed class SqliteReminderHistoryRepository(SqliteConnectionFactory connectionFactory)
    : IReminderHistoryRepository
{
    public async Task<bool> WasSentAsync(
        Guid birthdayId,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
              SELECT 1 FROM reminder_history
              WHERE birthday_id = $birthdayId AND scheduled_at = $scheduledAt);
            """;
        command.Parameters.AddWithValue("$birthdayId", birthdayId.ToString("D"));
        command.Parameters.AddWithValue("$scheduledAt", Format(scheduledAt));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task RecordSentAsync(
        ReminderCandidate candidate,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO reminder_history(
              birthday_id, scheduled_at, occurrence_date, sent_at)
            VALUES($birthdayId, $scheduledAt, $occurrenceDate, $sentAt);
            """;
        command.Parameters.AddWithValue("$birthdayId", candidate.BirthdayId.ToString("D"));
        command.Parameters.AddWithValue("$scheduledAt", Format(candidate.ScheduledAt));
        command.Parameters.AddWithValue(
            "$occurrenceDate",
            candidate.OccurrenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$sentAt", Format(sentAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string Format(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);
}
