using System.Windows;
using WpfSize = System.Windows.Size;

namespace QingLi.Windows.Views;

public sealed class CalendarPopupLayoutSession : IDisposable
{
    private static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromMilliseconds(300);

    private readonly ICalendarPopupLayoutStore _store;
    private readonly Func<IReadOnlyList<CalendarPopupPhysicalScreen>> _screensProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly TimeSpan _saveDelay;
    private readonly SynchronizationContext? _persistenceContext;
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
        Func<IReadOnlyList<CalendarPopupPhysicalScreen>> screensProvider,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        TimeSpan? saveDelay = null,
        SynchronizationContext? persistenceContext = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(screensProvider);

        _store = store;
        _screensProvider = screensProvider;
        _delayAsync = delayAsync ?? Task.Delay;
        _saveDelay = saveDelay ?? DefaultSaveDelay;
        _persistenceContext = persistenceContext ?? SynchronizationContext.Current;
        if (_saveDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(saveDelay));
        }
    }

    public event Action<Exception>? PersistenceFailed;

    public Exception? LastPersistenceError { get; private set; }

    public bool IsCustomized { get; private set; }

    public Task<Rect> InitializeAsync(
        Rect defaultPhysicalBounds,
        WpfSize minimumSizeInDips,
        double visibleDragHeightInDips,
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
            defaultPhysicalBounds,
            minimumSizeInDips,
            visibleDragHeightInDips,
            version,
            initializationCancellation,
            completion);
        return completion.Task;
    }

    public void RecordLayoutChange(Rect physicalBounds)
    {
        if (!IsValid(physicalBounds))
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
        }

        CalendarPopupLayout layout;
        try
        {
            layout = CalendarPopupScreenGeometry.ToPersistedLayout(
                physicalBounds,
                _screensProvider());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return;
        }

        CancellationTokenSource saveCancellation;
        TaskCompletionSource completion;
        long version;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_initialized)
            {
                return;
            }

            IsCustomized = true;
            version = ++_changeVersion;
            _pendingSaveCancellation?.Cancel();
            saveCancellation = new CancellationTokenSource();
            completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingSaveCancellation = saveCancellation;
            _pendingSaveTask = completion.Task;
        }

        _ = RunPersistenceAsync(layout, version, saveCancellation, completion);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? initializationCancellation;
        Task<Rect>? initialization;
        CancellationTokenSource? pendingSaveCancellation;
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
            pendingSaveCancellation = _pendingSaveCancellation;
            pendingSave = _pendingSaveTask;
        }

        await IgnoreCompletionAsync(initialization);
        await IgnoreCompletionAsync(pendingSave);

        lock (_sync)
        {
            if (ReferenceEquals(_initializationCancellation, initializationCancellation))
            {
                _initializationCancellation = null;
                _initializationTask = null;
            }

            if (ReferenceEquals(_pendingSaveCancellation, pendingSaveCancellation))
            {
                _pendingSaveCancellation = null;
                _pendingSaveTask = Task.CompletedTask;
            }
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

    private async Task RunInitializationAsync(
        Rect defaultPhysicalBounds,
        WpfSize minimumSizeInDips,
        double visibleDragHeightInDips,
        long version,
        CancellationTokenSource initializationCancellation,
        TaskCompletionSource<Rect> completion)
    {
        var cancellationToken = initializationCancellation.Token;
        try
        {
            var restored = defaultPhysicalBounds;
            var isCustomized = false;
            try
            {
                var saved = await _store.LoadAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (saved is { IsCustomized: true }
                    && IsValid(saved, minimumSizeInDips)
                    && !string.IsNullOrWhiteSpace(saved.MonitorDeviceName))
                {
                    restored = CalendarPopupScreenGeometry.RestoreSavedLayout(
                        saved,
                        _screensProvider(),
                        minimumSizeInDips,
                        visibleDragHeightInDips);
                    isCustomized = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                restored = defaultPhysicalBounds;
                isCustomized = false;
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

    private async Task RunPersistenceAsync(
        CalendarPopupLayout layout,
        long version,
        CancellationTokenSource saveCancellation,
        TaskCompletionSource completion)
    {
        var cancellationToken = saveCancellation.Token;
        try
        {
            await _delayAsync(_saveDelay, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await _store.SaveAsync(layout, cancellationToken);
            DispatchPersistenceResult(version, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DispatchPersistenceResult(version, exception);
        }
        finally
        {
            completion.TrySetResult();
            lock (_sync)
            {
                if (ReferenceEquals(_pendingSaveCancellation, saveCancellation))
                {
                    _pendingSaveCancellation = null;
                    _pendingSaveTask = Task.CompletedTask;
                }
            }

            saveCancellation.Dispose();
        }
    }

    private void DispatchPersistenceResult(long version, Exception? exception)
    {
        if (_persistenceContext is null)
        {
            ApplyPersistenceResult(version, exception);
            return;
        }

        _persistenceContext.Post(
            static state =>
            {
                var result = (PersistenceResult)state!;
                result.Session.ApplyPersistenceResult(result.Version, result.Exception);
            },
            new PersistenceResult(this, version, exception));
    }

    private void ApplyPersistenceResult(long version, Exception? exception)
    {
        lock (_sync)
        {
            if (_disposed || version != _changeVersion)
            {
                return;
            }

            LastPersistenceError = exception;
            if (exception is null)
            {
                return;
            }

            try
            {
                PersistenceFailed?.Invoke(exception);
            }
            catch
            {
                // Persistence is best-effort even if a subscriber fails.
            }
        }
    }

    private static async Task IgnoreCompletionAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch
        {
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

    private sealed record PersistenceResult(
        CalendarPopupLayoutSession Session,
        long Version,
        Exception? Exception);
}
