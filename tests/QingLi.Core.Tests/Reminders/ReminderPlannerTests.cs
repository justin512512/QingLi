using QingLi.Core.Birthdays;
using QingLi.Core.Reminders;

namespace QingLi.Core.Tests.Reminders;

public sealed class ReminderPlannerTests
{
    [Fact]
    public void Plans_reminder_at_configured_lead_time()
    {
        var birthday = Gregorian("小林", month: 8, day: 18, daysBefore: 3);
        var planner = new ReminderPlanner(new BirthdayOccurrenceService());

        var result = planner.DueBetween(
            [birthday],
            new DateTimeOffset(2027, 8, 15, 8, 59, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2027, 8, 15, 9, 1, 0, TimeSpan.FromHours(8)));

        Assert.Single(result);
        Assert.Equal(new DateOnly(2027, 8, 18), result[0].OccurrenceDate);
    }

    [Fact]
    public void Disabled_birthday_does_not_create_candidate()
    {
        var birthday = Gregorian("小林", 8, 18, 3) with { IsEnabled = false };
        var planner = new ReminderPlanner(new BirthdayOccurrenceService());

        var result = planner.DueBetween(
            [birthday],
            new DateTimeOffset(2027, 8, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2027, 8, 15, 10, 0, 0, TimeSpan.FromHours(8)));

        Assert.Empty(result);
    }

    [Fact]
    public void Cross_year_window_finds_early_january_reminder()
    {
        var birthday = Gregorian("小周", 1, 2, 3);
        var planner = new ReminderPlanner(new BirthdayOccurrenceService());

        var result = planner.DueBetween(
            [birthday],
            new DateTimeOffset(2026, 12, 29, 8, 59, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 12, 30, 9, 1, 0, TimeSpan.FromHours(8)));

        Assert.Single(result);
        Assert.Equal(new DateOnly(2027, 1, 2), result[0].OccurrenceDate);
        Assert.Equal(new DateOnly(2026, 12, 30), DateOnly.FromDateTime(result[0].ScheduledAt.DateTime));
    }

    private static Birthday Gregorian(
        string name,
        int month,
        int day,
        int daysBefore) =>
        new(Guid.NewGuid(), name, BirthdayCalendarKind.Gregorian, 1990, month, day, false,
            daysBefore, new TimeOnly(9, 0), null, true);
}
