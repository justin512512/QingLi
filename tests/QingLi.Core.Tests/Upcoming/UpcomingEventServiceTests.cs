using QingLi.Core.Almanac;
using QingLi.Core.Anniversaries;
using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.Holidays;
using QingLi.Core.Upcoming;

namespace QingLi.Core.Tests.Upcoming;

public sealed class UpcomingEventServiceTests
{
    [Fact]
    public async Task AggregatesAndOrdersEventsWithinHorizon()
    {
        var today = new DateOnly(2026, 7, 15);
        var eventDate = new DateOnly(2026, 7, 23);
        var birthday = new Birthday(
            Guid.NewGuid(), "小林", BirthdayCalendarKind.Gregorian,
            1990, 7, 23, false, 3, new TimeOnly(9, 0), null, true);
        var anniversary = new Anniversary(
            Guid.NewGuid(), "结婚纪念日", AnniversaryCalendarKind.Gregorian,
            2020, 7, 23, false, 3, new TimeOnly(9, 0), null, true);
        var service = new UpcomingEventService(
            new SolarTermService(),
            new HolidayService([new HolidayDefinition(eventDate, "测试假日", false)]),
            new FestivalAlmanac(eventDate),
            new BirthdayRepository([birthday]),
            new AnniversaryRepository([anniversary]),
            new BirthdayOccurrenceService(),
            new AnniversaryOccurrenceService());

        var actual = await service.GetUpcomingAsync(today, 90, default);
        var sameDay = actual.Where(item => item.Date == eventDate).ToArray();

        Assert.Equal(
            [
                UpcomingEventKind.Holiday,
                UpcomingEventKind.SolarTerm,
                UpcomingEventKind.Festival,
                UpcomingEventKind.Birthday,
                UpcomingEventKind.Anniversary
            ],
            sameDay.Select(item => item.Kind));
        Assert.All(sameDay, item => Assert.Equal(8, item.DaysRemaining));
        Assert.Equal(birthday.Id, sameDay.Single(item => item.Kind == UpcomingEventKind.Birthday).SubjectId);
        Assert.Equal(anniversary.Id, sameDay.Single(item => item.Kind == UpcomingEventKind.Anniversary).SubjectId);
        Assert.True(sameDay.Single(item => item.Kind == UpcomingEventKind.Holiday).IsRestDay);
    }

    [Fact]
    public async Task ExcludesDisabledAndBeyondHorizonEvents()
    {
        var today = new DateOnly(2026, 7, 15);
        var disabled = new Birthday(
            Guid.NewGuid(), "停用生日", BirthdayCalendarKind.Gregorian,
            1990, 7, 20, false, 3, new TimeOnly(9, 0), null, false);
        var tooLate = new Anniversary(
            Guid.NewGuid(), "远期纪念日", AnniversaryCalendarKind.Gregorian,
            2020, 12, 1, false, 3, new TimeOnly(9, 0), null, true);
        var service = new UpcomingEventService(
            new SolarTermService(),
            new HolidayService(),
            new EmptyAlmanac(),
            new BirthdayRepository([disabled]),
            new AnniversaryRepository([tooLate]),
            new BirthdayOccurrenceService(),
            new AnniversaryOccurrenceService());

        var actual = await service.GetUpcomingAsync(today, 30, default);

        Assert.DoesNotContain(actual, item => item.Title is "停用生日" or "远期纪念日");
        Assert.All(actual, item => Assert.InRange(item.DaysRemaining, 0, 30));
    }

    private sealed class BirthdayRepository(IReadOnlyList<Birthday> values) : IBirthdayRepository
    {
        public Task<IReadOnlyList<Birthday>> ListAsync(string? nameFilter, DateOnly today, CancellationToken cancellationToken) => Task.FromResult(values);
        public Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(values.FirstOrDefault(item => item.Id == id));
        public Task SaveAsync(Birthday birthday, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AnniversaryRepository(IReadOnlyList<Anniversary> values) : IAnniversaryRepository
    {
        public Task<IReadOnlyList<Anniversary>> ListAsync(string? titleFilter, DateOnly today, CancellationToken cancellationToken) => Task.FromResult(values);
        public Task<Anniversary?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(values.FirstOrDefault(item => item.Id == id));
        public Task SaveAsync(Anniversary anniversary, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FestivalAlmanac(DateOnly festivalDate) : IAlmanacService
    {
        public AlmanacDay GetDay(DateOnly date) => new(
            date, "", "", "", "", "", null,
            date == festivalDate ? ["测试节"] : [], [], []);
    }

    private sealed class EmptyAlmanac : IAlmanacService
    {
        public AlmanacDay GetDay(DateOnly date) => new(date, "", "", "", "", "", null, [], [], []);
    }
}
