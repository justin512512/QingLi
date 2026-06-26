namespace QingLi.Windows.Notifications;

public sealed class NotificationUnavailableException(string message) : InvalidOperationException(message);
