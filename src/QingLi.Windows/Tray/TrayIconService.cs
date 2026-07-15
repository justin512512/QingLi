using System.Drawing;
using System.Windows.Forms;

namespace QingLi.Windows.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _ownedIcon;
    private readonly Action _onToggleCalendar;
    private readonly Action _onAddBirthday;
    private readonly Action _onOpenSettings;
    private readonly Action _onPauseTodayReminders;
    private readonly Action _onRestoreSystemClock;
    private readonly Action _onExit;

    public TrayIconService(
        Action onToggleCalendar,
        Action onAddBirthday,
        Action onOpenSettings,
        Action onPauseTodayReminders,
        Action onRestoreSystemClock,
        Action onExit,
        Icon? icon = null)
    {
        _onToggleCalendar = onToggleCalendar;
        _onAddBirthday = onAddBirthday;
        _onOpenSettings = onOpenSettings;
        _onPauseTodayReminders = onPauseTodayReminders;
        _onRestoreSystemClock = onRestoreSystemClock;
        _onExit = onExit;

        ContextMenuStrip = BuildMenu();
        MenuTexts = ContextMenuStrip.Items
            .Cast<ToolStripItem>()
            .Select(item => item.Text ?? string.Empty)
            .ToArray();

        _ownedIcon = icon is null ? QingLiTrayIcon.Create() : null;
        _notifyIcon = new NotifyIcon
        {
            Icon = icon ?? _ownedIcon,
            Text = "轻历",
            Visible = true,
            ContextMenuStrip = ContextMenuStrip
        };
        _notifyIcon.MouseClick += HandleMouseClick;
    }

    public ContextMenuStrip ContextMenuStrip { get; }

    public IReadOnlyList<string> MenuTexts { get; }

    public event EventHandler? BalloonTipClicked
    {
        add => _notifyIcon.BalloonTipClicked += value;
        remove => _notifyIcon.BalloonTipClicked -= value;
    }

    public void HandlePrimaryClick() => _onToggleCalendar();

    public void ShowBalloonTip(string title, string text, int timeoutMilliseconds)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(timeoutMilliseconds);
    }

    public void Dispose()
    {
        _notifyIcon.MouseClick -= HandleMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _ownedIcon?.Dispose();
        ContextMenuStrip.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false
        };

        menu.Items.Add("打开日历", null, (_, _) => _onToggleCalendar());
        menu.Items.Add("添加生日", null, (_, _) => _onAddBirthday());
        menu.Items.Add("设置", null, (_, _) => _onOpenSettings());
        menu.Items.Add("暂停今日提醒", null, (_, _) => _onPauseTodayReminders());
        menu.Items.Add("恢复系统时钟", null, (_, _) => _onRestoreSystemClock());
        menu.Items.Add("退出", null, (_, _) => _onExit());
        return menu;
    }

    private void HandleMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            HandlePrimaryClick();
        }
    }
}
