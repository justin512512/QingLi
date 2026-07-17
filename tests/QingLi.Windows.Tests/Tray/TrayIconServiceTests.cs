using System.Buffers.Binary;
using System.Drawing;
using QingLi.Windows.Tray;

namespace QingLi.Windows.Tests.Tray;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void Brand_icon_assets_exist_and_primary_frame_is_256_pixels()
    {
        var root = GetRepositoryRoot();
        var source = Path.Combine(root, "src", "QingLi.Windows", "Assets", "Brand", "qingli-app-icon-source.png");
        var ico = Path.Combine(root, "src", "QingLi.Windows", "Assets", "Brand", "QingLi.ico");

        Assert.True(File.Exists(source));
        Assert.True(File.Exists(ico));
        using var icon = new Icon(ico);
        Assert.NotEqual(IntPtr.Zero, icon.Handle);

        var bytes = File.ReadAllBytes(ico);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        var frameCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        Assert.Equal((ushort)9, frameCount);

        var expectedSizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var directorySizes = new List<int>(frameCount);
        var payloadSizes = new List<int>(frameCount);
        for (var index = 0; index < frameCount; index++)
        {
            var entry = bytes.AsSpan(6 + (index * 16), 16);
            var directoryWidth = entry[0] == 0 ? 256 : entry[0];
            var directoryHeight = entry[1] == 0 ? 256 : entry[1];
            Assert.Equal(directoryWidth, directoryHeight);

            var payloadOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[12..16]));
            var png = bytes.AsSpan(payloadOffset);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }, png[..8].ToArray());
            var payloadWidth = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png[16..20]));
            var payloadHeight = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png[20..24]));
            Assert.Equal(payloadWidth, payloadHeight);
            Assert.Equal(directoryWidth, payloadWidth);

            directorySizes.Add(directoryWidth);
            payloadSizes.Add(payloadWidth);
        }

        Assert.Equal(expectedSizes, directorySizes.Order());
        Assert.Equal(expectedSizes, payloadSizes.Order());
    }

    [Fact]
    public void Default_tray_icon_is_branded_instead_of_the_generic_Windows_application_icon()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(), "src", "QingLi.Windows", "Tray", "TrayIconService.cs"));

        Assert.Contains("QingLiTrayIcon.Create", source);
        Assert.DoesNotContain("SystemIcons.Application", source);
    }

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

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
