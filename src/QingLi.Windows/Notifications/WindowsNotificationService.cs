using System.Security.Principal;
using System.Windows;
using QingLi.Core.Reminders;
using QingLi.Windows.Scheduling;
using QingLi.Windows.Tray;

namespace QingLi.Windows.Notifications;

public sealed class WindowsNotificationService :
    INotificationService,
    IReminderNotificationSink,
    IDisposable
{
    private readonly TrayIconService _trayIconService;
    private readonly Action<Guid>? _openBirthday;
    private bool _adminWarningShown;
    private Guid? _lastBirthdayId;

    public WindowsNotificationService(TrayIconService trayIconService, Action<Guid>? openBirthday = null)
    {
        _trayIconService = trayIconService;
        _openBirthday = openBirthday;
        _trayIconService.BalloonTipClicked += HandleBalloonTipClicked;
    }

    public bool IsAvailable => !IsProcessElevated();

    public Task SendAsync(ReminderCandidate candidate, CancellationToken cancellationToken) =>
        ShowBirthdayAsync(candidate, cancellationToken);

    public Task ShowBirthdayAsync(ReminderCandidate candidate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsProcessElevated())
        {
            ShowAdminWarningOnce();
            throw new NotificationUnavailableException(
                "通知功能需要普通用户运行。请退出后以普通用户身份重新启动轻历。");
        }

        var payload = NotificationPayloadBuilder.Build(candidate);
        _lastBirthdayId = candidate.BirthdayId;
        _trayIconService.ShowBalloonTip(payload.Title, payload.Body, 10_000);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _trayIconService.BalloonTipClicked -= HandleBalloonTipClicked;
    }

    private void HandleBalloonTipClicked(object? sender, EventArgs e)
    {
        if (_lastBirthdayId is { } birthdayId)
        {
            _openBirthday?.Invoke(birthdayId);
        }
    }

    public void ShowUnavailableWarning() => ShowAdminWarningOnce();

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

    public static bool IsProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
