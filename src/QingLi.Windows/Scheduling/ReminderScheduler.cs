using QingLi.Core.Birthdays;
using QingLi.Core.Reminders;
using Microsoft.Win32;

namespace QingLi.Windows.Scheduling;

public sealed class ReminderScheduler(
    IBirthdayRepository birthdays,
    ReminderPlanner planner,
    IReminderHistoryRepository history,
    IReminderSuppression suppression,
    IReminderNotificationSink notifications,
    DateTimeOffset lastSuccessfulCheck,
    Func<DateTimeOffset>? nowProvider = null)
    : IDisposable
{
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly Func<DateTimeOffset> _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
    private DateTimeOffset _lastSuccessfulCheck = lastSuccessfulCheck;
    private System.Timers.Timer? _timer;
    private bool _isStarted;
    private bool _isDisposed;

    public event Action<Exception>? CheckFailed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isStarted)
        {
            return;
        }

        _timer = new System.Timers.Timer(TimeSpan.FromMinutes(1))
        {
            AutoReset = true,
            Enabled = true
        };
        _timer.Elapsed += HandleTimerElapsed;
        SystemEvents.PowerModeChanged += HandlePowerModeChanged;
        SystemEvents.TimeChanged += HandleTimeChanged;
        _isStarted = true;
    }

    public async Task CheckAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            var localToday = DateOnly.FromDateTime(now.DateTime);
            var allBirthdays = await birthdays.ListAsync(null, localToday, cancellationToken);
            var candidates = planner.DueBetween(allBirthdays, _lastSuccessfulCheck, now);
            var suppressedDate = await suppression.GetSuppressedDateAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var scheduledDate = DateOnly.FromDateTime(candidate.ScheduledAt.DateTime);
                if (scheduledDate != localToday || suppressedDate == localToday)
                {
                    continue;
                }

                if (await history.WasSentAsync(
                    candidate.BirthdayId, candidate.ScheduledAt, cancellationToken))
                {
                    continue;
                }

                await notifications.SendAsync(candidate, cancellationToken);
                await history.RecordSentAsync(candidate, now, cancellationToken);
            }

            _lastSuccessfulCheck = now;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (!_isStarted)
        {
            _isDisposed = true;
            _checkLock.Dispose();
            return;
        }

        SystemEvents.PowerModeChanged -= HandlePowerModeChanged;
        SystemEvents.TimeChanged -= HandleTimeChanged;
        if (_timer is not null)
        {
            _timer.Elapsed -= HandleTimerElapsed;
            _timer.Dispose();
        }

        _isStarted = false;
        _isDisposed = true;
        _checkLock.Dispose();
    }

    private void HandleTimerElapsed(object? sender, System.Timers.ElapsedEventArgs args) =>
        QueueCheck();

    private void HandlePowerModeChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode == PowerModes.Resume)
        {
            QueueCheck();
        }
    }

    private void HandleTimeChanged(object? sender, EventArgs args) => QueueCheck();

    private async void QueueCheck()
    {
        try
        {
            await CheckAsync(_nowProvider(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            CheckFailed?.Invoke(exception);
        }
    }
}
