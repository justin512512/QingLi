using QingLi.Core.Birthdays;

namespace QingLi.Core.Reminders;

public sealed class ReminderPlanner(BirthdayOccurrenceService occurrences)
{
    public IReadOnlyList<ReminderCandidate> DueBetween(
        IReadOnlyList<Birthday> birthdays,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        if (to < from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The end must not precede the start.");
        }

        var candidates = new List<ReminderCandidate>();
        var firstOccurrenceYear = from.Year;
        var lastOccurrenceYear = to.Year + 1;

        foreach (var birthday in birthdays.Where(value => value.IsEnabled))
        {
            for (var year = firstOccurrenceYear; year <= lastOccurrenceYear; year++)
            {
                var occurrence = occurrences.GetOccurrence(birthday, year);
                var local = occurrence.AddDays(-birthday.ReminderDaysBefore)
                    .ToDateTime(birthday.ReminderTime);
                var scheduled = new DateTimeOffset(local, to.Offset);

                if (scheduled > from && scheduled <= to)
                {
                    candidates.Add(new ReminderCandidate(
                        birthday.Id, birthday.Name, occurrence, scheduled));
                }
            }
        }

        return candidates.OrderBy(value => value.ScheduledAt).ToArray();
    }
}
