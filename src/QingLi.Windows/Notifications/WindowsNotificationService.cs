using System.Drawing;
using System.Security.Principal;
using System.Windows;
using System.Windows.Forms;
using QingLi.Core.Reminders;
using QingLi.Windows.Scheduling;

namespace QingLi.Windows.Notifications;

public sealed class WindowsNotificationService :
    INotificationService,
    IReminderNotificationSink,
    IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Action<Guid>? _openBirthday;
    private bool _adminWarningShown;
    private Guid? _lastBirthdayId;

    public WindowsNotificationService(Action<Guid>? openBirthday = null, Icon? icon = null)
    {
        _openBirthday = openBirthday;
        _notifyIcon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Text = "轻历",
            Visible = true
        };
        _notifyIcon.BalloonTipClicked += HandleBalloonTipClicked;
    }

    public Task SendAsync(ReminderCandidate candidate, CancellationToken cancellationToken) =>
        ShowBirthdayAsync(candidate, cancellationToken);

    public Task ShowBirthdayAsync(ReminderCandidate candidate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsProcessElevated())
        {
            ShowAdminWarningOnce();
            return Task.CompletedTask;
        }

        var payload = NotificationPayloadBuilder.Build(candidate);
        _lastBirthdayId = candidate.BirthdayId;
        _notifyIcon.BalloonTipTitle = payload.Title;
        _notifyIcon.BalloonTipText = payload.Body;
        _notifyIcon.ShowBalloonTip(10_000);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _notifyIcon.BalloonTipClicked -= HandleBalloonTipClicked;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void HandleBalloonTipClicked(object? sender, EventArgs e)
    {
        if (_lastBirthdayId is { } birthdayId)
        {
            _openBirthday?.Invoke(birthdayId);
        }
    }

    private void ShowAdminWarningOnce()
    {
        if (_adminWarningShown)
        {
            return;
        }

        _adminWarningShown = true;
        System.Windows.MessageBox.Show(
            "通知功能需要普通用户运行。请退出后以普通用户身份重新启动轻历。",
            "轻历",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static bool IsProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
