using System.Globalization;
using QingLi.Core.Reminders;

namespace QingLi.Windows.Notifications;

public static class NotificationPayloadBuilder
{
    public static NotificationPayload Build(ReminderCandidate value)
    {
        var date = value.OccurrenceDate.ToString("M月d日", CultureInfo.InvariantCulture);
        return value.SubjectKind switch
        {
            ReminderSubjectKind.Birthday => new NotificationPayload(
                $"{value.Name}的生日提醒",
                $"{value.Name}的生日是 {date}",
                $"action=open-birthday&birthdayId={value.SubjectId:D}"),
            ReminderSubjectKind.Anniversary => new NotificationPayload(
                $"{value.Name}纪念日提醒",
                $"{value.Name}是 {date}",
                $"action=open-anniversary&anniversaryId={value.SubjectId:D}"),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value.SubjectKind, null)
        };
    }
}
