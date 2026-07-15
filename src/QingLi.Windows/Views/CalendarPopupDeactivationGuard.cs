namespace QingLi.Windows.Views;

public sealed class CalendarPopupDeactivationGuard(
    TimeSpan gracePeriod,
    Func<DateTimeOffset>? now = null)
{
    private readonly TimeSpan _gracePeriod = gracePeriod >= TimeSpan.Zero
        ? gracePeriod
        : throw new ArgumentOutOfRangeException(nameof(gracePeriod));
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.Now);
    private DateTimeOffset? _shownAt;

    public void MarkShown() => _shownAt = _now();

    public bool ShouldClose() =>
        _shownAt is { } shownAt && _now() - shownAt >= _gracePeriod;
}
