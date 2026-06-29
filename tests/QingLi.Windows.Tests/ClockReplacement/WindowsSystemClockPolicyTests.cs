using QingLi.Core.ClockReplacement;
using QingLi.Windows.ClockReplacement;

namespace QingLi.Windows.Tests.ClockReplacement;

public sealed class WindowsSystemClockPolicyTests
{
    [Fact]
    public async Task Capture_records_missing_original_value()
    {
        var registry = new FakeUserRegistry();
        var policy = new WindowsSystemClockPolicy(registry, new FakeShellSettingsBroadcaster());

        var state = await policy.CaptureAsync(default);

        Assert.False(state.ValueExisted);
        Assert.Null(state.OriginalValue);
    }

    [Fact]
    public async Task Capture_records_existing_original_value()
    {
        var registry = new FakeUserRegistry();
        registry.SetInt32(WindowsSystemClockPolicy.PolicyPath, WindowsSystemClockPolicy.ValueName, 7);
        var policy = new WindowsSystemClockPolicy(registry, new FakeShellSettingsBroadcaster());

        var state = await policy.CaptureAsync(default);

        Assert.True(state.ValueExisted);
        Assert.Equal(7, state.OriginalValue);
    }

    [Fact]
    public async Task Hide_sets_policy_value_and_broadcasts_change()
    {
        var registry = new FakeUserRegistry();
        var broadcaster = new FakeShellSettingsBroadcaster();
        var policy = new WindowsSystemClockPolicy(registry, broadcaster);

        await policy.HideAsync(default);

        Assert.True(registry.TryGetInt32(
            WindowsSystemClockPolicy.PolicyPath,
            WindowsSystemClockPolicy.ValueName,
            out var value));
        Assert.Equal(1, value);
        Assert.Equal(1, broadcaster.BroadcastCount);
    }

    [Fact]
    public async Task Restore_deletes_value_when_it_did_not_exist_before()
    {
        var registry = new FakeUserRegistry();
        var broadcaster = new FakeShellSettingsBroadcaster();
        var policy = new WindowsSystemClockPolicy(registry, broadcaster);
        var state = new SystemClockState(false, null, DateTimeOffset.UtcNow);

        await policy.HideAsync(default);
        await policy.RestoreAsync(state, default);

        Assert.False(registry.TryGetInt32(
            WindowsSystemClockPolicy.PolicyPath,
            WindowsSystemClockPolicy.ValueName,
            out _));
        Assert.Equal(2, broadcaster.BroadcastCount);
    }

    [Fact]
    public async Task Restore_writes_original_value_when_it_existed_before()
    {
        var registry = new FakeUserRegistry();
        var policy = new WindowsSystemClockPolicy(registry, new FakeShellSettingsBroadcaster());
        var state = new SystemClockState(true, 3, DateTimeOffset.UtcNow);

        await policy.HideAsync(default);
        await policy.RestoreAsync(state, default);

        Assert.True(registry.TryGetInt32(
            WindowsSystemClockPolicy.PolicyPath,
            WindowsSystemClockPolicy.ValueName,
            out var value));
        Assert.Equal(3, value);
    }

    private sealed class FakeUserRegistry : IUserRegistry
    {
        private readonly Dictionary<(string Path, string Name), int> _values = [];

        public bool TryGetInt32(string path, string name, out int value) =>
            _values.TryGetValue((path, name), out value);

        public void SetInt32(string path, string name, int value) =>
            _values[(path, name)] = value;

        public void DeleteValue(string path, string name) =>
            _values.Remove((path, name));
    }

    private sealed class FakeShellSettingsBroadcaster : IShellSettingsBroadcaster
    {
        public int BroadcastCount { get; private set; }

        public void BroadcastPolicyChanged() => BroadcastCount++;
    }
}
