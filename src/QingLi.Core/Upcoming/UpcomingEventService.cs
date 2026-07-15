using QingLi.Core.Almanac;
using QingLi.Core.Anniversaries;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.Holidays;

namespace QingLi.Core.Upcoming;

public sealed class UpcomingEventService(
    SolarTermService solarTerms,
    HolidayService holidays,
    IAlmanacService almanac,
    IBirthdayRepository birthdays,
    IAnniversaryRepository anniversaries,
    BirthdayOccurrenceService birthdayOccurrences,
    AnniversaryOccurrenceService anniversaryOccurrences) : IUpcomingEventService
{
    public async Task<IReadOnlyList<UpcomingEvent>> GetUpcomingAsync(
        DateOnly today,
        int horizonDays,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horizonDays);
        var end = today.AddDays(horizonDays);
        var result = new List<UpcomingEvent>();

        for (var offset = 0; offset <= horizonDays; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var date = today.AddDays(offset);
            var holiday = holidays.Find(date);
            if (holiday is not null)
            {
                result.Add(new UpcomingEvent(
                    date,
                    offset,
                    UpcomingEventKind.Holiday,
                    holiday.Name,
                    !holiday.IsWorkday));
            }

            var solarTerm = solarTerms.GetName(date);
            if (solarTerm is not null)
            {
                result.Add(new UpcomingEvent(
                    date,
                    offset,
                    UpcomingEventKind.SolarTerm,
                    solarTerm));
            }

            foreach (var festival in almanac.GetDay(date).Festivals.Distinct(StringComparer.Ordinal))
            {
                result.Add(new UpcomingEvent(
                    date,
                    offset,
                    UpcomingEventKind.Festival,
                    festival));
            }
        }

        var birthdayValues = await birthdays.ListAsync(null, today, cancellationToken);
        foreach (var birthday in birthdayValues.Where(item => item.IsEnabled))
        {
            AddOccurrences(
                result,
                today,
                end,
                birthday.Id,
                birthday.Name,
                UpcomingEventKind.Birthday,
                year => birthdayOccurrences.GetOccurrence(birthday, year));
        }

        var anniversaryValues = await anniversaries.ListAsync(null, today, cancellationToken);
        foreach (var anniversary in anniversaryValues.Where(item => item.IsEnabled))
        {
            AddOccurrences(
                result,
                today,
                end,
                anniversary.Id,
                anniversary.Title,
                UpcomingEventKind.Anniversary,
                year => anniversaryOccurrences.GetOccurrence(anniversary, year));
        }

        return result
            .DistinctBy(item => (item.Date, item.Kind, item.Title, item.SubjectId))
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Title, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static void AddOccurrences(
        ICollection<UpcomingEvent> result,
        DateOnly today,
        DateOnly end,
        Guid subjectId,
        string title,
        UpcomingEventKind kind,
        Func<int, DateOnly> occurrenceForYear)
    {
        for (var year = today.Year; year <= end.Year; year++)
        {
            var occurrence = occurrenceForYear(year);
            if (occurrence < today || occurrence > end)
            {
                continue;
            }

            result.Add(new UpcomingEvent(
                occurrence,
                occurrence.DayNumber - today.DayNumber,
                kind,
                title,
                SubjectId: subjectId));
        }
    }
}
