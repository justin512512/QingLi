using System.Collections.Concurrent;
using System.Windows;
using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupLayoutSessionTests
{
    private static readonly CalendarPopupPhysicalScreen Primary = new(
        @"\\.\DISPLAY1",
        new Rect(0, 0, 1920, 1080),
        new Rect(0, 0, 1920, 1040),
        96,
        96);
    private static readonly Rect DefaultBounds = new(880, 520, 1040, 520);
    private static readonly Size MinimumSize = new(760, 420);

    [Fact]
    public async Task CustomizedMonitorLocalLayoutWinsAndIsConstrainedPhysically()
    {
        var store = new FakeStore
        {
            Loaded = new CalendarPopupLayout(
                1700, 900, 1040, 520, true, Primary.DeviceName)
        };
        using var session = CreateSession(store);

        var actual = await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        Assert.Equal(new Rect(880, 520, 1040, 520), actual);
        Assert.True(session.IsCustomized);
        Assert.Empty(store.Saved);
    }

    [Theory]
    [MemberData(nameof(UnusableLayouts))]
    public async Task MissingInvalidNoncustomOrLegacyLayoutUsesPhysicalDefault(
        CalendarPopupLayout? saved)
    {
        var store = new FakeStore { Loaded = saved };
        using var session = CreateSession(store);

        var actual = await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        Assert.Equal(DefaultBounds, actual);
        Assert.False(session.IsCustomized);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ProgrammaticInitializationDoesNotSave()
    {
        var store = new FakeStore();
        var delay = new ControlledDelay();
        using var session = CreateSession(store, delay.DelayAsync);

        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        Assert.Equal(0, delay.Count);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task RepeatedPhysicalChangesSaveOnlyFinalMonitorLocalLayout()
    {
        var store = new FakeStore();
        var delay = new ControlledDelay();
        using var session = CreateSession(store, delay.DelayAsync);
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);
        session.RecordLayoutChange(new Rect(30, 40, 920, 540));
        await delay.WaitForCountAsync(2);
        session.RecordLayoutChange(new Rect(50, 60, 940, 560));
        await delay.WaitForCountAsync(3);
        delay.Complete(2);
        await store.WaitForSaveAttemptCountAsync(1);

        Assert.Equal(
            new CalendarPopupLayout(50, 60, 940, 560, true, Primary.DeviceName),
            Assert.Single(store.Saved));
        Assert.Equal(TimeSpan.FromMilliseconds(300), delay.RequestedDelays[2]);
        Assert.True(session.IsCustomized);
    }

    [Fact]
    public async Task LoadFailureFallsBackAndStillAllowsUserChangesToPersist()
    {
        var store = new FakeStore { LoadFailure = new IOException("read failed") };
        var delay = new ControlledDelay();
        using var session = CreateSession(store, delay.DelayAsync);

        var actual = await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        session.RecordLayoutChange(new Rect(100, 120, 900, 500));
        await delay.WaitForCountAsync(1);
        delay.Complete(0);
        await store.WaitForSaveAttemptCountAsync(1);

        Assert.Equal(DefaultBounds, actual);
        Assert.Equal(Primary.DeviceName, Assert.Single(store.Saved).MonitorDeviceName);
    }

    [Fact]
    public async Task GeometryFailureFallsBackAndStillAllowsUserChangesToPersist()
    {
        var store = new FakeStore
        {
            Loaded = new CalendarPopupLayout(100, 120, 900, 500, true, Primary.DeviceName)
        };
        var delay = new ControlledDelay();
        var calls = 0;
        using var session = new CalendarPopupLayoutSession(
            store,
            () => Interlocked.Increment(ref calls) == 1
                ? throw new InvalidOperationException("screen query failed")
                : [Primary],
            delay.DelayAsync);

        var actual = await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        session.RecordLayoutChange(new Rect(100, 120, 900, 500));
        await delay.WaitForCountAsync(1);
        delay.Complete(0);
        await store.WaitForSaveAttemptCountAsync(1);

        Assert.Equal(DefaultBounds, actual);
        Assert.Single(store.Saved);
    }

    [Fact]
    public async Task PersistenceFailureIsDeliveredOnInjectedContext()
    {
        var failure = new IOException("disk unavailable");
        var store = new FakeStore();
        store.SaveFailures.Enqueue(failure);
        var delay = new ControlledDelay();
        var context = new QueuedSynchronizationContext();
        using var session = CreateSession(store, delay.DelayAsync, context);
        Exception? published = null;
        var eventThread = -1;
        session.PersistenceFailed += exception =>
        {
            published = exception;
            eventThread = Environment.CurrentManagedThreadId;
        };
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);

        await Task.Run(() => delay.Complete(0));
        await store.WaitForSaveAttemptCountAsync(1);
        await WaitUntilAsync(() => context.Count == 1);

        Assert.Null(published);
        var dispatchThread = Environment.CurrentManagedThreadId;
        context.Drain();
        Assert.Same(failure, published);
        Assert.Equal(dispatchThread, eventThread);
        Assert.Same(failure, session.LastPersistenceError);
    }

    [Fact]
    public async Task SupersededQueuedFailureIsNotPublished()
    {
        var store = new FakeStore();
        store.SaveFailures.Enqueue(new IOException("stale failure"));
        var delay = new ControlledDelay();
        var context = new QueuedSynchronizationContext();
        using var session = CreateSession(store, delay.DelayAsync, context);
        var published = new List<Exception>();
        session.PersistenceFailed += published.Add;
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);
        delay.Complete(0);
        await store.WaitForSaveAttemptCountAsync(1);
        await WaitUntilAsync(() => context.Count == 1);
        session.RecordLayoutChange(new Rect(30, 40, 920, 540));
        await delay.WaitForCountAsync(2);
        context.Drain();

        Assert.Empty(published);
        Assert.Null(session.LastPersistenceError);
    }

    [Fact]
    public async Task DisposedQueuedFailureIsNotPublished()
    {
        var store = new FakeStore();
        store.SaveFailures.Enqueue(new IOException("disposed failure"));
        var delay = new ControlledDelay();
        var context = new QueuedSynchronizationContext();
        var session = CreateSession(store, delay.DelayAsync, context);
        var published = new List<Exception>();
        session.PersistenceFailed += published.Add;
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);
        delay.Complete(0);
        await store.WaitForSaveAttemptCountAsync(1);
        await WaitUntilAsync(() => context.Count == 1);

        session.Dispose();
        context.Drain();

        Assert.Empty(published);
        Assert.Null(session.LastPersistenceError);
    }

    [Fact]
    public async Task LaterSuccessfulSaveClearsCurrentPersistenceError()
    {
        var failure = new IOException("first save failed");
        var store = new FakeStore();
        store.SaveFailures.Enqueue(failure);
        var delay = new ControlledDelay();
        var context = new QueuedSynchronizationContext();
        using var session = CreateSession(store, delay.DelayAsync, context);
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);
        delay.Complete(0);
        await store.WaitForSaveAttemptCountAsync(1);
        await WaitUntilAsync(() => context.Count == 1);
        context.Drain();
        Assert.Same(failure, session.LastPersistenceError);

        session.RecordLayoutChange(new Rect(30, 40, 920, 540));
        await delay.WaitForCountAsync(2);
        delay.Complete(1);
        await store.WaitForSaveAttemptCountAsync(2);
        await WaitUntilAsync(() => context.Count == 1);
        context.Drain();

        Assert.Null(session.LastPersistenceError);
    }

    [Fact]
    public async Task ResetCancelsPendingDebounceWithoutSaving()
    {
        var store = new FakeStore();
        var delay = new ControlledDelay();
        using var session = CreateSession(store, delay.DelayAsync);
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);

        await session.ResetAsync();

        Assert.Empty(store.Saved);
        Assert.True(delay.IsCanceled(0));
        Assert.False(session.IsCustomized);
    }

    [Fact]
    public async Task DisposeCancelsPendingDebounceWithoutSaving()
    {
        var store = new FakeStore();
        var delay = new ControlledDelay();
        var session = CreateSession(store, delay.DelayAsync);
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);

        session.Dispose();

        Assert.True(delay.IsCanceled(0));
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ResetCancelsAndAwaitsActiveRestoreBeforeClearingAndDefaulting()
    {
        var saved = new CalendarPopupLayout(
            100, 120, 900, 500, true, Primary.DeviceName);
        var store = new ControlledLoadStore(saved);
        using var session = new CalendarPopupLayoutSession(store, () => [Primary]);
        var initialization = session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        await store.LoadStarted;

        var reset = session.ResetAsync();
        await WaitUntilAsync(() => store.FirstLoadToken.IsCancellationRequested);
        Assert.False(reset.IsCompleted);
        store.CompleteFirstLoad();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        await reset;
        Assert.Equal(1, store.ClearCount);
        Assert.False(session.IsCustomized);
        Assert.Equal(
            DefaultBounds,
            await session.InitializeAsync(DefaultBounds, MinimumSize, 28));
    }

    public static TheoryData<CalendarPopupLayout?> UnusableLayouts => new()
    {
        null,
        new CalendarPopupLayout(20, 30, 900, 500, false, Primary.DeviceName),
        new CalendarPopupLayout(double.NaN, 30, 900, 500, true, Primary.DeviceName),
        new CalendarPopupLayout(20, 30, 759, 500, true, Primary.DeviceName),
        new CalendarPopupLayout(20, 30, 900, 419, true, Primary.DeviceName),
        new CalendarPopupLayout(1280, 0, 1040, 520, true),
        new CalendarPopupLayout(20, 30, 1040, 520, true, @"\\.\DISCONNECTED")
    };

    private static CalendarPopupLayoutSession CreateSession(
        FakeStore store,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        SynchronizationContext? context = null) =>
        new(store, () => [Primary], delay, persistenceContext: context);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeStore : ICalendarPopupLayoutStore
    {
        private readonly SemaphoreSlim _saveSignal = new(0);
        private int _saveAttempts;

        public CalendarPopupLayout? Loaded { get; init; }
        public Exception? LoadFailure { get; init; }
        public List<CalendarPopupLayout> Saved { get; } = [];
        public ConcurrentQueue<Exception> SaveFailures { get; } = new();

        public Task<CalendarPopupLayout?> LoadAsync(CancellationToken cancellationToken) =>
            LoadFailure is null
                ? Task.FromResult(Loaded)
                : Task.FromException<CalendarPopupLayout?>(LoadFailure);

        public Task SaveAsync(CalendarPopupLayout layout, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _saveAttempts);
            if (SaveFailures.TryDequeue(out var failure))
            {
                _saveSignal.Release();
                return Task.FromException(failure);
            }

            Saved.Add(layout);
            _saveSignal.Release();
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task WaitForSaveAttemptCountAsync(int count)
        {
            while (Volatile.Read(ref _saveAttempts) < count)
            {
                Assert.True(await _saveSignal.WaitAsync(TimeSpan.FromSeconds(2)));
            }
        }
    }

    private sealed class ControlledDelay
    {
        private readonly List<TaskCompletionSource> _requests = [];
        private readonly List<CancellationToken> _tokens = [];

        public int Count { get { lock (_requests) return _requests.Count; } }
        public IReadOnlyList<TimeSpan> RequestedDelays { get; } = new List<TimeSpan>();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_requests)
            {
                ((List<TimeSpan>)RequestedDelays).Add(delay);
                _requests.Add(completion);
                _tokens.Add(cancellationToken);
            }

            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return completion.Task;
        }

        public void Complete(int index)
        {
            lock (_requests) _requests[index].TrySetResult();
        }

        public bool IsCanceled(int index)
        {
            lock (_requests) return _tokens[index].IsCancellationRequested;
        }

        public async Task WaitForCountAsync(int count)
        {
            for (var attempt = 0; attempt < 100 && Count < count; attempt++)
            {
                await Task.Delay(10);
            }

            Assert.True(Count >= count);
        }
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_callbacks) _callbacks.Enqueue((d, state));
        }

        public int Count { get { lock (_callbacks) return _callbacks.Count; } }

        public void Drain()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) item;
                lock (_callbacks)
                {
                    if (_callbacks.Count == 0) return;
                    item = _callbacks.Dequeue();
                }

                item.Callback(item.State);
            }
        }
    }

    private sealed class ControlledLoadStore(CalendarPopupLayout saved)
        : ICalendarPopupLayoutStore
    {
        private readonly TaskCompletionSource<CalendarPopupLayout?> _firstLoad =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _loadStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _loadCount;
        private bool _cleared;

        public Task LoadStarted => _loadStarted.Task;
        public CancellationToken FirstLoadToken { get; private set; }
        public int ClearCount { get; private set; }

        public Task<CalendarPopupLayout?> LoadAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _loadCount) == 1)
            {
                FirstLoadToken = cancellationToken;
                _loadStarted.TrySetResult();
                return _firstLoad.Task;
            }

            return Task.FromResult<CalendarPopupLayout?>(_cleared ? null : saved);
        }

        public Task SaveAsync(CalendarPopupLayout layout, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            _cleared = true;
            ClearCount++;
            return Task.CompletedTask;
        }

        public void CompleteFirstLoad() => _firstLoad.TrySetResult(saved);
    }
}
