using QingLi.Core.Reminders;
using QingLi.Windows.Notifications;

namespace QingLi.Windows.Tests.Notifications;

public sealed class NotificationPayloadTests
{
    [Fact]
    public void Birthday_payload_contains_name_date_and_action()
    {
        var candidate = ReminderSamples.For("小林", new DateOnly(2027, 8, 18));

        var payload = NotificationPayloadBuilder.Build(candidate);

        Assert.Contains("小林", payload.Title);
        Assert.Contains("8月18日", payload.Body);
        Assert.Equal($"action=open-birthday&birthdayId={candidate.BirthdayId:D}", payload.Arguments);
    }

    private static class ReminderSamples
    {
        public static ReminderCandidate For(string name, DateOnly occurrenceDate) =>
            new(Guid.NewGuid(), name, occurrenceDate, occurrenceDate.ToDateTimeOffset());
    }
}

internal static class DateOnlyTestExtensions
{
    public static DateTimeOffset ToDateTimeOffset(this DateOnly value) =>
        new(value.ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(8));
}
