using QingLi.Core.ClockReplacement;
using QingLi.Core.Settings;
using QingLi.Windows.ClockReplacement;

namespace QingLi.Windows.Tests.ClockReplacement;

public sealed class ClockReplacementCoordinatorTests
{
    private static readonly SystemClockState Snapshot =
        new(false, null, DateTimeOffset.Parse("2026-06-30T10:00:00+08:00"));

    [Fact]
    public async Task Enable_uses_strict_safe_order_and_persists_enabled_last()
    {
        var fixture = new Fixture();

        var result = await fixture.Coordinator.SetEnabledAsync(true, AppSettings.Default, default);

        Assert.True(result.Succeeded);
        Assert.True(result.Settings.ReplaceSystemClock);
        Assert.Equal(
            ["capture", "snapshot-save", "window-show", "policy-hide", "settings-True"],
            fixture.Calls);
    }

    [Fact]
    public async Task Enable_failure_restores_before_hiding_window_and_deletes_snapshot_last()
    {
        var fixture = new Fixture { HideException = new InvalidOperationException("hide failed") };

        var result = await fixture.Coordinator.SetEnabledAsync(true, AppSettings.Default, default);

        Assert.False(result.Succeeded);
        Assert.False(result.Settings.ReplaceSystemClock);
        Assert.Equal(
            [
                "capture", "snapshot-save", "window-show", "policy-hide",
                "policy-restore", "window-hide", "settings-False", "snapshot-delete"
            ],
            fixture.Calls);
        Assert.False(fixture.StateStore.HasSnapshot);
    }

    [Fact]
    public async Task Enabled_setting_save_failure_rolls_back_system_clock_and_window()
    {
        var fixture = new Fixture
        {
            EnabledSettingsSaveException = new InvalidOperationException("settings failed")
        };

        var result = await fixture.Coordinator.SetEnabledAsync(true, AppSettings.Default, default);

        Assert.False(result.Succeeded);
        Assert.Equal(
            [
                "capture", "snapshot-save", "window-show", "policy-hide", "settings-True",
                "policy-restore", "window-hide", "settings-False", "snapshot-delete"
            ],
            fixture.Calls);
        Assert.False(fixture.StateStore.HasSnapshot);
    }

    [Fact]
    public async Task Failed_rollback_keeps_snapshot_and_does_not_hide_custom_window()
    {
        var fixture = new Fixture
        {
            HideException = new InvalidOperationException("hide failed"),
            RestoreException = new InvalidOperationException("restore failed")
        };

        var result = await fixture.Coordinator.SetEnabledAsync(true, AppSettings.Default, default);

        Assert.False(result.Succeeded);
        Assert.True(fixture.StateStore.HasSnapshot);
        Assert.DoesNotContain("snapshot-delete", fixture.Calls);
        Assert.DoesNotContain("window-hide", fixture.Calls);
        Assert.True(result.Settings.ReplaceSystemClock);
    }

    [Fact]
    public async Task Cancellation_after_snapshot_save_rolls_back_with_non_cancelable_token_then_propagates()
    {
        var fixture = new Fixture { CancelShow = true };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        fixture.ShowCancellationToken = cancellation.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Coordinator.SetEnabledAsync(true, AppSettings.Default, CancellationToken.None));

        Assert.Equal(
            [
                "capture", "snapshot-save", "window-show", "policy-restore",
                "window-hide", "settings-False", "snapshot-delete"
            ],
            fixture.Calls);
        Assert.Equal(CancellationToken.None, Assert.Single(fixture.RestoreTokens));
    }

    [Fact]
    public async Task Disable_restores_policy_before_hiding_window_and_clears_snapshot_after_save()
    {
        var fixture = new Fixture(snapshot: Snapshot);
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.SetEnabledAsync(false, enabled, default);

        Assert.True(result.Succeeded);
        Assert.False(result.Settings.ReplaceSystemClock);
        Assert.Equal(
            ["snapshot-load", "policy-restore", "window-hide", "settings-False", "snapshot-delete"],
            fixture.Calls);
    }

    [Fact]
    public async Task Startup_with_orphan_snapshot_restores_without_rewriting_false_setting()
    {
        var fixture = new Fixture(snapshot: Snapshot);

        var result = await fixture.Coordinator.RecoverOnStartupAsync(AppSettings.Default, default);

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["snapshot-load", "policy-restore", "window-hide", "snapshot-delete"],
            fixture.Calls);
    }

    [Fact]
    public async Task Startup_window_failure_restores_and_disables_setting()
    {
        var fixture = new Fixture(snapshot: Snapshot) { ShowSucceeds = false };
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.RecoverOnStartupAsync(enabled, default);

        Assert.False(result.Succeeded);
        Assert.False(result.Settings.ReplaceSystemClock);
        Assert.Equal(
            [
                "snapshot-load", "window-show", "policy-restore", "window-hide",
                "settings-False", "snapshot-delete"
            ],
            fixture.Calls);
    }

    [Fact]
    public async Task Startup_window_exception_restores_and_disables_setting()
    {
        var fixture = new Fixture(snapshot: Snapshot)
        {
            ShowException = new InvalidOperationException("window failed")
        };
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.RecoverOnStartupAsync(enabled, default);

        Assert.False(result.Succeeded);
        Assert.False(result.Settings.ReplaceSystemClock);
        Assert.True(result.ErrorMessage?.Contains("window failed", StringComparison.Ordinal));
        Assert.Equal(
            [
                "snapshot-load", "window-show", "policy-restore", "window-hide",
                "settings-False", "snapshot-delete"
            ],
            fixture.Calls);
    }

    [Fact]
    public async Task Startup_cancellation_after_existing_snapshot_rolls_back_before_propagating()
    {
        var fixture = new Fixture(snapshot: Snapshot) { CancelShow = true };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        fixture.ShowCancellationToken = cancellation.Token;
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Coordinator.RecoverOnStartupAsync(enabled, CancellationToken.None));

        Assert.Equal(
            [
                "snapshot-load", "window-show", "policy-restore", "window-hide",
                "settings-False", "snapshot-delete"
            ],
            fixture.Calls);
        Assert.Equal(CancellationToken.None, Assert.Single(fixture.RestoreTokens));
    }

    [Fact]
    public async Task Missing_snapshot_uses_emergency_unhide_before_hiding_custom_clock()
    {
        var fixture = new Fixture();
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.RecoverOnStartupAsync(enabled, default);

        Assert.False(result.Succeeded);
        Assert.False(result.Settings.ReplaceSystemClock);
        Assert.Contains("紧急策略", result.ErrorMessage);
        Assert.Equal(
            ["snapshot-load", "policy-restore", "window-hide", "settings-False", "snapshot-delete"],
            fixture.Calls);
    }

    [Fact]
    public async Task Corrupt_snapshot_with_failed_emergency_unhide_preserves_custom_clock()
    {
        var fixture = new Fixture
        {
            LoadException = new InvalidDataException("corrupt"),
            RestoreException = new InvalidOperationException("policy failed")
        };
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.RecoverOnStartupAsync(enabled, default);

        Assert.False(result.Succeeded);
        Assert.True(result.Settings.ReplaceSystemClock);
        Assert.Equal(["snapshot-load", "policy-restore", "window-show"], fixture.Calls);
        Assert.DoesNotContain("window-hide", fixture.Calls);
        Assert.DoesNotContain("snapshot-delete", fixture.Calls);
    }

    [Fact]
    public async Task Disabled_settings_save_failure_still_returns_runtime_disabled_and_deletes_snapshot()
    {
        var fixture = new Fixture(snapshot: Snapshot)
        {
            DisabledSettingsSaveException = new InvalidOperationException("save false failed")
        };
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.SetEnabledAsync(false, enabled, default);

        Assert.False(result.Succeeded);
        Assert.False(result.Settings.ReplaceSystemClock);
        Assert.False(fixture.StateStore.HasSnapshot);
        Assert.Equal(
            ["snapshot-load", "policy-restore", "window-hide", "settings-False", "snapshot-delete"],
            fixture.Calls);

        fixture.Calls.Clear();
        var nextStartup = await fixture.Coordinator.RecoverOnStartupAsync(enabled, default);
        Assert.False(nextStartup.Settings.ReplaceSystemClock);
        Assert.DoesNotContain("policy-hide", fixture.Calls);
    }

    [Fact]
    public async Task Snapshot_delete_failure_still_returns_runtime_disabled_after_settings_save()
    {
        var fixture = new Fixture(snapshot: Snapshot)
        {
            DeleteException = new IOException("delete failed")
        };
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.SetEnabledAsync(false, enabled, default);

        Assert.False(result.Succeeded);
        Assert.False(result.Settings.ReplaceSystemClock);
        Assert.True(fixture.StateStore.HasSnapshot);
        Assert.Equal(
            ["snapshot-load", "policy-restore", "window-hide", "settings-False", "snapshot-delete"],
            fixture.Calls);

        fixture.Calls.Clear();
        fixture.DeleteException = null;
        var nextStartup = await fixture.Coordinator.RecoverOnStartupAsync(result.Settings, default);
        Assert.False(nextStartup.Settings.ReplaceSystemClock);
        Assert.DoesNotContain("policy-hide", fixture.Calls);
    }

    [Fact]
    public async Task Triple_persistence_failure_keeps_unsafe_flag_and_blocks_exit()
    {
        var fixture = new Fixture(snapshot: Snapshot)
        {
            DisabledSettingsSaveException = new IOException("settings failed"),
            DeleteException = new IOException("delete failed"),
            MarkerSaveException = new IOException("marker failed")
        };
        var enabled = AppSettings.Default with { ReplaceSystemClock = true };

        var result = await fixture.Coordinator.SetEnabledAsync(false, enabled, default);

        Assert.False(result.Succeeded);
        Assert.True(result.Settings.ReplaceSystemClock);
        Assert.True(fixture.StateStore.HasSnapshot);
        Assert.Equal(
            [
                "snapshot-load", "policy-restore", "window-hide", "settings-False",
                "snapshot-delete", "snapshot-save"
            ],
            fixture.Calls);
    }

    [Fact]
    public async Task Incompatible_enable_never_captures_or_mutates_policy()
    {
        var fixture = new Fixture(compatible: false);

        var result = await fixture.Coordinator.SetEnabledAsync(true, AppSettings.Default, default);

        Assert.False(result.Succeeded);
        Assert.Empty(fixture.Calls);
    }

    private sealed class Fixture
    {
        public Fixture(bool compatible = true, SystemClockState? snapshot = null)
        {
            StateStore = new FakeStateStore(Calls, snapshot) { Fixture = this };
            Coordinator = new ClockReplacementCoordinator(
                new FakeCompatibility(compatible),
                new FakePolicy(this),
                StateStore,
                new FakeWindow(this),
                new FakeSettingsStore(this));
        }

        public List<string> Calls { get; } = [];
        public FakeStateStore StateStore { get; }
        public ClockReplacementCoordinator Coordinator { get; }
        public bool ShowSucceeds { get; set; } = true;
        public Exception? ShowException { get; set; }
        public Exception? HideException { get; set; }
        public Exception? RestoreException { get; set; }
        public Exception? EnabledSettingsSaveException { get; set; }
        public Exception? DisabledSettingsSaveException { get; set; }
        public Exception? LoadException { get; set; }
        public Exception? DeleteException { get; set; }
        public Exception? MarkerSaveException { get; set; }
        public bool CancelShow { get; set; }
        public CancellationToken ShowCancellationToken { get; set; }
        public List<CancellationToken> RestoreTokens { get; } = [];
    }

    private sealed class FakeCompatibility(bool compatible) : IClockReplacementCompatibility
    {
        public bool IsCompatible => compatible;
        public string Message => compatible ? "compatible" : "incompatible";
    }

    private sealed class FakePolicy(Fixture fixture) : ISystemClockPolicy
    {
        public Task<SystemClockState> CaptureAsync(CancellationToken cancellationToken)
        {
            fixture.Calls.Add("capture");
            return Task.FromResult(Snapshot);
        }

        public Task HideAsync(CancellationToken cancellationToken)
        {
            fixture.Calls.Add("policy-hide");
            return fixture.HideException is null
                ? Task.CompletedTask
                : Task.FromException(fixture.HideException);
        }

        public Task RestoreAsync(SystemClockState state, CancellationToken cancellationToken)
        {
            fixture.Calls.Add("policy-restore");
            fixture.RestoreTokens.Add(cancellationToken);
            return fixture.RestoreException is null
                ? Task.CompletedTask
                : Task.FromException(fixture.RestoreException);
        }
    }

    private sealed class FakeStateStore(
        List<string> calls, SystemClockState? initialState) : ISystemClockStateStore
    {
        private SystemClockState? _state = initialState;
        public bool HasSnapshot => _state is not null;

        public Task<SystemClockState?> LoadAsync(CancellationToken cancellationToken)
        {
            calls.Add("snapshot-load");
            if (Fixture?.LoadException is not null)
            {
                return Task.FromException<SystemClockState?>(Fixture.LoadException);
            }

            return Task.FromResult(_state);
        }

        public Task SaveAsync(SystemClockState state, CancellationToken cancellationToken)
        {
            calls.Add("snapshot-save");
            if (state.CapturedAt == DateTimeOffset.MinValue && Fixture?.MarkerSaveException is not null)
            {
                return Task.FromException(Fixture.MarkerSaveException);
            }

            _state = state;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CancellationToken cancellationToken)
        {
            calls.Add("snapshot-delete");
            if (Fixture?.DeleteException is not null)
            {
                return Task.FromException(Fixture.DeleteException);
            }

            _state = null;
            return Task.CompletedTask;
        }

        public Fixture? Fixture { get; set; }
    }

    private sealed class FakeWindow(Fixture fixture) : IClockWindowController
    {
        public Task<bool> ShowAsync(CancellationToken cancellationToken)
        {
            fixture.Calls.Add("window-show");
            if (fixture.CancelShow)
            {
                return Task.FromCanceled<bool>(fixture.ShowCancellationToken);
            }
            if (fixture.ShowException is not null)
            {
                return Task.FromException<bool>(fixture.ShowException);
            }

            return Task.FromResult(fixture.ShowSucceeds);
        }

        public void Hide() => fixture.Calls.Add("window-hide");
    }

    private sealed class FakeSettingsStore(Fixture fixture) : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(AppSettings.Default);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            fixture.Calls.Add($"settings-{settings.ReplaceSystemClock}");
            if (settings.ReplaceSystemClock && fixture.EnabledSettingsSaveException is not null)
            {
                return Task.FromException(fixture.EnabledSettingsSaveException);
            }

            if (!settings.ReplaceSystemClock && fixture.DisabledSettingsSaveException is not null)
            {
                return Task.FromException(fixture.DisabledSettingsSaveException);
            }

            return Task.CompletedTask;
        }
    }
}
