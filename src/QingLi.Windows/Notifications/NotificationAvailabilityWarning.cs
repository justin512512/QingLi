namespace QingLi.Windows.Notifications;

public sealed class NotificationAvailabilityWarning(Action present)
{
    private readonly Action _present = present ?? throw new ArgumentNullException(nameof(present));
    private bool _shown;

    public void ShowOnce()
    {
        if (_shown)
        {
            return;
        }

        _shown = true;
        _present();
    }
}
