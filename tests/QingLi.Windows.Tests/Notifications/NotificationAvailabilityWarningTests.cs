using QingLi.Windows.Notifications;

namespace QingLi.Windows.Tests.Notifications;

public sealed class NotificationAvailabilityWarningTests
{
    [Fact]
    public void Warning_is_presented_non_blockingly_only_once()
    {
        var presentations = 0;
        var warning = new NotificationAvailabilityWarning(() => presentations++);

        warning.ShowOnce();
        warning.ShowOnce();

        Assert.Equal(1, presentations);
    }
}
