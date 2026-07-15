using System.Windows;
using WpfSize = System.Windows.Size;

namespace QingLi.Windows.Views;

public sealed class CalendarPopupLayoutSession : IDisposable
{
    private static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromMilliseconds(300);

    private readonly ICalendarPopupLayoutStore _store;
    private readonly Func<IReadOnlyList<Rect>> _workAreasProvider;
    private readonly Func<Rect> _fallbackWorkAreaProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly TimeSpan _saveDelay;
    private readonly object _sync = new();

    private CancellationTokenSource? _pendingSaveCancellation;
    private Task _pendingSaveTask = Task.CompletedTask;
    private CancellationTokenSource? _initializationCancellation;
    private Task<Rect>? _initializationTask;
    private long _initializationVersion;
    private long _changeVersion;
    private bool _initialized;
    private bool _disposed;

    public CalendarPopupLayoutSession(
        ICalendarPopupLayoutStore store,
        Func<IReadOnlyList<Rect>> workAreasProvider,
        Func<Rect> fallbackWorkAreaProvider,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        TimeSpan? saveDelay = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(workAreasProvider);
        ArgumentNullException.ThrowIfNull(fallbackWorkAreaProvider);

        _store = store;
        _workAreasProvider = workAreasProvider;
        _fallbackWorkAreaProvider = fallbackWorkAreaProvider;
        _delayAsync = delayAsync ?? Task.Delay;
        _saveDelay = saveDelay ?? DefaultSaveDelay;
        if (_saveDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(saveDelay));
        }
    }

    public event Action<Exception>? PersistenceFailed;

    public Exception? LastPersistenceError { get; private set; }

    public bool IsCustomized { get; private set; }

    public Task<Rect> InitializeAsync(
        Rect defaultBounds,
        WpfSize minimumSize,
        double visibleDragHeight,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource initializationCancellation;
        TaskCompletionSource<Rect> completion;
        long version;
        lock (_sync)
        {
            ThrowIfDisposed();
            _initializationCancellation?.Cancel();
            initializationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            completion = new TaskCompletionSource<Rect>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            version = ++_initializationVersion;
            _initializationCancellation = initializationCancellation;
            _initializationTask = completion.Task;
        }

        _ = RunInitializationAsync(
            defaultBounds,
            minimumSize,
            visibleDragHeight,
            version,
            initializationCancellation,
            completion);
        return completion.Task;
    }

    private async Task RunInitializationAsync(
        Rect defaultBounds,
        WpfSize minimumSize,
        double visibleDragHeight,
        long version,
        CancellationTokenSource initializationCancellation,
        TaskCompletionSource<Rect> completion)
    {
        var cancellationToken = initializationCancellation.Token;
        try
        {
            var saved = await _store.LoadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var restored = defaultBounds;
            var isCustomized = false;
            if (saved is { IsCustomized: true } && IsValid(saved, minimumSize))
            {
                try
                {
                    restored = CalendarPopupPlacement.ConstrainSaved(
                        new Rect(saved.Left, saved.Top, saved.Width, saved.Height),
                        _workAreasProvider(),
                        _fallbackWorkAreaProvider(),
                        minimumSize,
                        visibleDragHeight);
                    isCustomized = true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    restored = defaultBounds;
                }
            }

            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (version != _initializationVersion)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                ThrowIfDisposed();
                IsCustomized = isCustomized;
                _initialized = true;
            }

            completion.TrySetResult(restored);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            lock (_sync)
            {
                if (version == _initializationVersion)
                {
                    _initializationCancellation = null;
                    _initializationTask = null;
                }
            }

            initializationCancellation.Dispose();
        }
    }

    public void RecordLayoutChange(Rect bounds)
    {
        if (!IsValid(bounds))
        {
            return;
        }

        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_initialized)
            {
                return;
            }

            IsCustomized = true;
            var version = ++_changeVersion;
            _pendingSaveCancellation?.Cancel();
            _pendingSaveCancellation = new CancellationTokenSource();
            var layout = new CalendarPopupLayout(
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    true);
            var cancellationToken = _pendingSaveCancellation.Token;
            _pendingSaveTask = Task.Run(
                () => PersistAfterDelayAsync(layout, version, cancellationToken));
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? initializationCancellation;
        Task<Rect>? initialization;
        Task pendingSave;
        lock (_sync)
        {
            ThrowIfDisposed();
            _initializationVersion++;
            _initializationCancellation?.Cancel();
            initializationCancellation = _initializationCancellation;
            initialization = _initializationTask;
            _initialized = false;
            IsCustomized = false;
            _changeVersion++;
            _pendingSaveCancellation?.Cancel();
            pendingSave = _pendingSaveTask;
        }

        if (initialization is not null)
        {
            try
            {
                await initialization;
            }
            catch
            {
                // Reset still owns the final state if restore was canceled or failed.
            }
        }

        lock (_sync)
        {
            if (ReferenceEquals(_initializationCancellation, initializationCancellation))
            {
                _initializationCancellation = null;
                _initializationTask = null;
            }
        }

        try
        {
            await pendingSave;
        }
        catch (OperationCanceledException)
        {
        }

        await _store.ClearAsync(cancellationToken);
        LastPersistenceError = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _initialized = false;
            _initializationVersion++;
            _initializationCancellation?.Cancel();
            _changeVersion++;
            _pendingSaveCancellation?.Cancel();
        }
    }

    private async Task PersistAfterDelayAsync(
        CalendarPopupLayout layout,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            await _delayAsync(_saveDelay, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await _store.SaveAsync(layout, cancellationToken);
            if (IsCurrent(version))
            {
                LastPersistenceError = null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrent(version))
            {
                LastPersistenceError = exception;
            }

            try
            {
                PersistenceFailed?.Invoke(exception);
            }
            catch
            {
                // Persistence remains best-effort even if a subscriber fails.
            }
        }
    }

    private bool IsCurrent(long version)
    {
        lock (_sync)
        {
            return !_disposed && version == _changeVersion;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static bool IsValid(CalendarPopupLayout layout, WpfSize minimumSize) =>
        double.IsFinite(layout.Left)
        && double.IsFinite(layout.Top)
        && double.IsFinite(layout.Width)
        && double.IsFinite(layout.Height)
        && layout.Width >= minimumSize.Width
        && layout.Height >= minimumSize.Height;

    private static bool IsValid(Rect bounds) =>
        !bounds.IsEmpty
        && double.IsFinite(bounds.Left)
        && double.IsFinite(bounds.Top)
        && double.IsFinite(bounds.Width)
        && double.IsFinite(bounds.Height)
        && bounds.Width > 0
        && bounds.Height > 0;
}
