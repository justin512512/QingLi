using System.Collections.Concurrent;
using System.Windows;
using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupLayoutSessionTests
{
    private static readonly Rect PrimaryWorkArea = new(0, 0, 1920, 1040);
    private static readonly Rect DefaultBounds = new(868, 508, 1040, 520);
    private static readonly Size MinimumSize = new(760, 420);

    [Fact]
    public async Task CustomizedSavedLayoutWinsAndIsConstrainedToCurrentScreens()
    {
        var store = new FakeStore
        {
            Loaded = new CalendarPopupLayout(1700, 900, 1040, 520, true)
        };
        using var session = CreateSession(store);

        var actual = await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        Assert.Equal(new Rect(880, 520, 1040, 520), actual);
        Assert.True(session.IsCustomized);
        Assert.Empty(store.Saved);
    }

    [Theory]
    [MemberData(nameof(UnusableLayouts))]
    public async Task MissingInvalidOrNoncustomLayoutUsesCallerDefault(CalendarPopupLayout? saved)
    {
        var store = new FakeStore { Loaded = saved };
        using var session = CreateSession(store);

        var actual = await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        Assert.Equal(DefaultBounds, actual);
        Assert.False(session.IsCustomized);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ProgrammaticInitialPositioningDoesNotSave()
    {
        var store = new FakeStore();
        var delay = new ControlledDelay();
        using var session = CreateSession(store, delay.DelayAsync);

        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        Assert.Equal(0, delay.Count);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task RepeatedChangesSaveOnlyFinalBoundsAfterIdleDelay()
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
        await store.WaitForSaveCountAsync(1);

        var saved = Assert.Single(store.Saved);
        Assert.Equal(new CalendarPopupLayout(50, 60, 940, 560, true), saved);
        Assert.Equal(TimeSpan.FromMilliseconds(300), delay.RequestedDelays[2]);
        Assert.True(session.IsCustomized);
    }

    [Fact]
    public async Task ResetCancelsPendingSaveClearsStoreAndCustomization()
    {
        var store = new FakeStore
        {
            Loaded = new CalendarPopupLayout(10, 20, 900, 500, true)
        };
        var delay = new ControlledDelay();
        using var session = CreateSession(store, delay.DelayAsync);
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        session.RecordLayoutChange(new Rect(30, 40, 920, 540));
        await delay.WaitForCountAsync(1);

        await session.ResetAsync();

        Assert.Equal(1, store.ClearCount);
        Assert.False(session.IsCustomized);
        Assert.True(delay.IsCanceled(0));
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task SaveFailureIsPublishedAndLaterSuccessClearsError()
    {
        var failure = new IOException("disk unavailable");
        var store = new FakeStore();
        store.SaveFailures.Enqueue(failure);
        var delay = new ControlledDelay();
        using var session = CreateSession(store, delay.DelayAsync);
        var published = new List<Exception>();
        session.PersistenceFailed += published.Add;
        await session.InitializeAsync(DefaultBounds, MinimumSize, 28);

        session.RecordLayoutChange(new Rect(10, 20, 900, 500));
        await delay.WaitForCountAsync(1);
        delay.Complete(0);
        await store.WaitForSaveAttemptCountAsync(1);
        await WaitUntilAsync(() => session.LastPersistenceError is not null);

        Assert.Same(failure, session.LastPersistenceError);
        Assert.Equal([failure], published);

        session.RecordLayoutChange(new Rect(30, 40, 920, 540));
        await delay.WaitForCountAsync(2);
        delay.Complete(1);
        await store.WaitForSaveAttemptCountAsync(2);
        await WaitUntilAsync(() => session.LastPersistenceError is null);

        Assert.Null(session.LastPersistenceError);
        Assert.Equal(new CalendarPopupLayout(30, 40, 920, 540, true), Assert.Single(store.Saved));
    }

    [Fact]
    public async Task DisposeCancelsPendingWorkWithoutSaving()
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
        var saved = new CalendarPopupLayout(100, 120, 900, 500, true);
        var store = new ControlledLoadStore(saved);
        using var session = new CalendarPopupLayoutSession(
            store,
            () => [PrimaryWorkArea],
            () => PrimaryWorkArea);

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

        var afterReset = await session.InitializeAsync(DefaultBounds, MinimumSize, 28);
        Assert.Equal(DefaultBounds, afterReset);
    }

    public static TheoryData<CalendarPopupLayout?> UnusableLayouts => new()
    {
        null,
        new CalendarPopupLayout(20, 30, 900, 500, false),
        new CalendarPopupLayout(double.NaN, 30, 900, 500, true),
        new CalendarPopupLayout(20, 30, 0, 500, true),
        new CalendarPopupLayout(20, 30, 759, 500, true),
        new CalendarPopupLayout(20, 30, 900, 419, true)
    };

    private static CalendarPopupLayoutSession CreateSession(
        FakeStore store,
        Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        new(
            store,
            () => [PrimaryWorkArea],
            () => PrimaryWorkArea,
            delay);

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
        public List<CalendarPopupLayout> Saved { get; } = [];
        public ConcurrentQueue<Exception> SaveFailures { get; } = new();
        public int ClearCount { get; private set; }

        public Task<CalendarPopupLayout?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Loaded);

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

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            ClearCount++;
            return Task.CompletedTask;
        }

        public async Task WaitForSaveCountAsync(int count)
        {
            while (Saved.Count < count)
            {
                await _saveSignal.WaitAsync(TimeSpan.FromSeconds(2));
            }
        }

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

        public int Count
        {
            get
            {
                lock (_requests) return _requests.Count;
            }
        }

        public IReadOnlyList<TimeSpan> RequestedDelays { get; } = new List<TimeSpan>();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
