using QingLi.Windows.Tray;

namespace QingLi.Windows.Tests.Tray;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void Tray_menu_texts_match_brief()
    {
        var service = new TrayIconService(
            onToggleCalendar: () => { },
            onAddBirthday: () => { },
            onOpenSettings: () => { },
            onPauseTodayReminders: () => { },
            onExit: () => { });

        Assert.Equal(
            ["打开日历", "添加生日", "设置", "暂停今日提醒", "退出"],
            service.MenuTexts);
    }

    [Fact]
    public void Left_click_invokes_toggle_calendar()
    {
        var toggles = 0;
        var service = new TrayIconService(
            onToggleCalendar: () => toggles++,
            onAddBirthday: () => { },
            onOpenSettings: () => { },
            onPauseTodayReminders: () => { },
            onExit: () => { });

        service.HandlePrimaryClick();

        Assert.Equal(1, toggles);
    }
}
