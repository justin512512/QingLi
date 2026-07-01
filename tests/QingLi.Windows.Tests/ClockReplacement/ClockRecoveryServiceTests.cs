using QingLi.Core.ClockReplacement;
using QingLi.Windows.ClockReplacement;

namespace QingLi.Windows.Tests.ClockReplacement;

public sealed class ClockRecoveryServiceTests
{
    [Fact]
    public async Task Snapshot_is_restored_then_deleted()
    {
        var state = new SystemClockState(true, 0, DateTimeOffset.UtcNow);
        var policy = new FakePolicy();
        var store = new FakeStore(state);

        var result = await new ClockRecoveryService(policy, store).RestoreAsync(default);

        Assert.True(result.Succeeded);
        Assert.Equal(state, policy.RestoredState);
        Assert.True(store.Deleted);
    }

    [Fact]
    public async Task Missing_snapshot_uses_emergency_unhide()
    {
        var policy = new FakePolicy();
        var store = new FakeStore(null);

        var result = await new ClockRecoveryService(policy, store).RestoreAsync(default);

        Assert.True(result.Succeeded);
        Assert.False(policy.RestoredState!.ValueExisted);
        Assert.Null(policy.RestoredState.OriginalValue);
        Assert.Contains("紧急恢复", result.Message);
    }

    private sealed class FakePolicy : ISystemClockPolicy
    {
        public SystemClockState? RestoredState { get; private set; }
        public Task<SystemClockState> CaptureAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task HideAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RestoreAsync(SystemClockState state, CancellationToken cancellationToken)
        {
            RestoredState = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStore(SystemClockState? state) : ISystemClockStateStore
    {
        public bool Deleted { get; private set; }
        public Task<SystemClockState?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(state);
        public Task SaveAsync(SystemClockState value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(CancellationToken cancellationToken)
        {
            Deleted = true;
            return Task.CompletedTask;
        }
    }
}
