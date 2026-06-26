using System.Globalization;
using QingLi.Core.Reminders;

namespace QingLi.Windows.Notifications;

public static class NotificationPayloadBuilder
{
    public static NotificationPayload Build(ReminderCandidate value) =>
        new(
            $"{value.Name}的生日提醒",
            $"{value.Name}的生日是 {value.OccurrenceDate.ToString("M月d日", CultureInfo.InvariantCulture)}",
            $"action=open-birthday&birthdayId={value.BirthdayId:D}");
}
