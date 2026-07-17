using System.Buffers.Binary;
using System.Drawing;
using System.Xml.Linq;
using QingLi.Windows.Tray;

namespace QingLi.Windows.Tests.Tray;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void Project_files_assign_the_brand_icon()
    {
        var root = GetRepositoryRoot();
        var windowsProject = XDocument.Load(Path.Combine(
            root, "src", "QingLi.Windows", "QingLi.Windows.csproj"));
        var recoveryProject = XDocument.Load(Path.Combine(
            root, "src", "QingLi.Recovery", "QingLi.Recovery.csproj"));

        Assert.Equal(
            @"Assets\Brand\QingLi.ico",
            windowsProject.Descendants("ApplicationIcon").Single().Value);
        Assert.Contains(
            windowsProject.Descendants("Resource"),
            resource => (string?)resource.Attribute("Include") == @"Assets\Brand\QingLi.ico");
        Assert.Equal(
            @"..\QingLi.Windows\Assets\Brand\QingLi.ico",
            recoveryProject.Descendants("ApplicationIcon").Single().Value);
    }

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

    [Theory]
    [InlineData(96, 16)]
    [InlineData(120, 20)]
    [InlineData(144, 24)]
    [InlineData(192, 32)]
    [InlineData(240, 40)]
    [InlineData(288, 48)]
    public void Tray_icon_size_tracks_system_Dpi(int dpi, int expectedSize)
    {
        Assert.Equal(expectedSize, QingLiTrayIcon.SelectIconSize(dpi));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(40)]
    [InlineData(48)]
    public void Tray_icon_is_loaded_from_the_embedded_Wpf_resource_and_matches_the_requested_frame(int size)
    {
        var ico = Path.Combine(
            GetRepositoryRoot(), "src", "QingLi.Windows", "Assets", "Brand", "QingLi.ico");

        using var actual = QingLiTrayIcon.Create(size);
        using var expected = new Icon(ico, size, size);

        Assert.NotEqual(IntPtr.Zero, actual.Handle);
        Assert.Equal(GetPixels(expected), GetPixels(actual));
    }

    [Fact]
    public void Tray_icon_clones_the_icon_before_disposing_the_resource_stream()
    {
        var ico = Path.Combine(
            GetRepositoryRoot(), "src", "QingLi.Windows", "Assets", "Brand", "QingLi.ico");
        var stream = new MemoryStream(File.ReadAllBytes(ico));

        using var icon = QingLiTrayIcon.Create(32, () => stream);

        Assert.False(stream.CanRead);
        Assert.NotEqual(IntPtr.Zero, icon.Handle);
        using var bitmap = icon.ToBitmap();
        Assert.Equal(32, bitmap.Width);
        Assert.Equal(32, bitmap.Height);
    }

    [Fact]
    public void Tray_icon_source_does_not_draw_or_create_native_icon_handles_at_runtime()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(), "src", "QingLi.Windows", "Tray", "QingLiTrayIcon.cs"));

        Assert.DoesNotContain("FillRoundedRectangle", source);
        Assert.DoesNotContain("GetHicon", source);
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

    private static int[] GetPixels(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        Assert.Equal(bitmap.Width, bitmap.Height);

        return Enumerable.Range(0, bitmap.Height)
            .SelectMany(y => Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, y).ToArgb()))
            .ToArray();
    }
}
