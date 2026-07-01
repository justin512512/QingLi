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
            onRestoreSystemClock: () => { },
            onExit: () => { });

        Assert.Equal(
            ["打开日历", "添加生日", "设置", "暂停今日提醒", "恢复系统时钟", "退出"],
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
            onRestoreSystemClock: () => { },
            onExit: () => { });

        service.HandlePrimaryClick();

        Assert.Equal(1, toggles);
    }

    [Fact]
    public void Birthday_menu_item_invokes_callback()
    {
        var addBirthday = 0;
        var service = new TrayIconService(
            onToggleCalendar: () => { },
            onAddBirthday: () => addBirthday++,
            onOpenSettings: () => { },
            onPauseTodayReminders: () => { },
            onRestoreSystemClock: () => { },
            onExit: () => { });

        service.ContextMenuStrip.Items[1].PerformClick();

        Assert.Equal(1, addBirthday);
    }

    [Fact]
    public void Settings_menu_item_invokes_callback()
    {
        var openSettings = 0;
        var service = new TrayIconService(
            onToggleCalendar: () => { },
            onAddBirthday: () => { },
            onOpenSettings: () => openSettings++,
            onPauseTodayReminders: () => { },
            onRestoreSystemClock: () => { },
            onExit: () => { });

        service.ContextMenuStrip.Items[2].PerformClick();

        Assert.Equal(1, openSettings);
    }

    [Fact]
    public void Restore_system_clock_menu_item_is_always_present_and_invokes_callback()
    {
        var restores = 0;
        using var service = new TrayIconService(
            onToggleCalendar: () => { },
            onAddBirthday: () => { },
            onOpenSettings: () => { },
            onPauseTodayReminders: () => { },
            onRestoreSystemClock: () => restores++,
            onExit: () => { });

        Assert.Contains("恢复系统时钟", service.MenuTexts);
        service.ContextMenuStrip.Items[4].PerformClick();
        Assert.Equal(1, restores);
    }
}
