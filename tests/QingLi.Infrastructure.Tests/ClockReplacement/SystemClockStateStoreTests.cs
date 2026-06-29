using QingLi.Core.ClockReplacement;
using QingLi.Infrastructure.ClockReplacement;

namespace QingLi.Infrastructure.Tests.ClockReplacement;

public sealed class SystemClockStateStoreTests : IDisposable
{
    private readonly string _temp = Path.Combine(
        Path.GetTempPath(), "QingLi.Tests", Guid.NewGuid().ToString("N"));

    public SystemClockStateStoreTests() => Directory.CreateDirectory(_temp);

    [Fact]
    public async Task Missing_snapshot_returns_null()
    {
        var store = new SystemClockStateStore(Path.Combine(_temp, "system-clock-state.json"));

        Assert.Null(await store.LoadAsync(default));
    }

    [Fact]
    public async Task Saved_snapshot_round_trips()
    {
        var store = new SystemClockStateStore(Path.Combine(_temp, "system-clock-state.json"));
        var expected = new SystemClockState(true, 0, DateTimeOffset.Parse("2026-06-29T12:30:00+08:00"));

        await store.SaveAsync(expected, default);

        Assert.Equal(expected, await store.LoadAsync(default));
    }

    [Fact]
    public async Task Delete_removes_saved_snapshot()
    {
        var path = Path.Combine(_temp, "system-clock-state.json");
        var store = new SystemClockStateStore(path);
        await store.SaveAsync(new SystemClockState(false, null, DateTimeOffset.UtcNow), default);

        await store.DeleteAsync(default);

        Assert.False(File.Exists(path));
        Assert.Null(await store.LoadAsync(default));
    }

    [Fact]
    public async Task Null_snapshot_is_rejected_as_invalid()
    {
        var path = Path.Combine(_temp, "system-clock-state.json");
        await File.WriteAllTextAsync(path, "null");
        var store = new SystemClockStateStore(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(default));
    }

    [Fact]
    public async Task Incomplete_snapshot_is_rejected_as_invalid()
    {
        var path = Path.Combine(_temp, "system-clock-state.json");
        await File.WriteAllTextAsync(path, "{}");
        var store = new SystemClockStateStore(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(default));
    }

    [Fact]
    public async Task Inconsistent_snapshot_is_rejected_as_invalid()
    {
        var path = Path.Combine(_temp, "system-clock-state.json");
        await File.WriteAllTextAsync(
            path,
            """{"valueExisted":false,"originalValue":1,"capturedAt":"2026-06-29T12:30:00+08:00"}""");
        var store = new SystemClockStateStore(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(default));
    }

    [Fact]
    public async Task Missing_snapshot_honors_pre_canceled_token()
    {
        var store = new SystemClockStateStore(Path.Combine(_temp, "system-clock-state.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.LoadAsync(cancellation.Token));
    }

    [Fact]
    public async Task Save_overwrites_existing_snapshot()
    {
        var store = new SystemClockStateStore(Path.Combine(_temp, "system-clock-state.json"));
        await store.SaveAsync(new SystemClockState(false, null, DateTimeOffset.UtcNow), default);
        var replacement = new SystemClockState(true, 4, DateTimeOffset.UtcNow.AddMinutes(1));

        await store.SaveAsync(replacement, default);

        Assert.Equal(replacement, await store.LoadAsync(default));
    }

    [Fact]
    public async Task Canceled_save_preserves_existing_snapshot_and_cleans_temporary_file()
    {
        var path = Path.Combine(_temp, "system-clock-state.json");
        var store = new SystemClockStateStore(path);
        var existing = new SystemClockState(true, 2, DateTimeOffset.UtcNow);
        await store.SaveAsync(existing, default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(new SystemClockState(false, null, DateTimeOffset.UtcNow), cancellation.Token));

        Assert.Equal(existing, await store.LoadAsync(default));
        Assert.Empty(Directory.GetFiles(_temp, "*.tmp"));
    }

    [Fact]
    public async Task Pre_canceled_save_does_not_create_target_directory()
    {
        var directory = Path.Combine(_temp, "not-created");
        var store = new SystemClockStateStore(Path.Combine(directory, "system-clock-state.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(new SystemClockState(false, null, DateTimeOffset.UtcNow), cancellation.Token));

        Assert.False(Directory.Exists(directory));
    }

    public void Dispose() => Directory.Delete(_temp, true);
}
