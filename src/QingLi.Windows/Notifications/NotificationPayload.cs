namespace QingLi.Windows.Notifications;

public sealed record NotificationPayload(
    string Title,
    string Body,
    string Arguments);
