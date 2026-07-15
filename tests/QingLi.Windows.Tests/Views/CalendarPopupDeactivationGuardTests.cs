using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupDeactivationGuardTests
{
    [Fact]
    public void Immediate_startup_deactivation_does_not_close_popup()
    {
        var now = new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(8));
        var guard = new CalendarPopupDeactivationGuard(
            TimeSpan.FromMilliseconds(750),
            () => now);

        guard.MarkShown();

        Assert.False(guard.ShouldClose());
    }

    [Fact]
    public void Later_user_deactivation_closes_popup()
    {
        var now = new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(8));
        var guard = new CalendarPopupDeactivationGuard(
            TimeSpan.FromMilliseconds(750),
            () => now);
        guard.MarkShown();
        now = now.AddSeconds(1);

        Assert.True(guard.ShouldClose());
    }
}
