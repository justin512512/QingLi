using QingLi.Core.Settings;
using QingLi.Windows.ClockReplacement;

namespace QingLi.Windows.Tests.ClockReplacement;

public sealed class ClockReplacementExitOrchestratorTests
{
    [Fact]
    public async Task Exit_restores_before_shutdown()
    {
        var calls = new List<string>();
        var coordinator = new FakeCoordinator(
            calls,
            new ClockReplacementResult(
                true, AppSettings.Default with { ReplaceSystemClock = false }));
        var orchestrator = new ClockReplacementExitOrchestrator(
            coordinator,
            () => AppSettings.Default with { ReplaceSystemClock = true },
            _ => calls.Add("settings-updated"),
            _ => calls.Add("warning"),
            () => calls.Add("shutdown"));

        var exited = await orchestrator.TryExitAsync();

        Assert.True(exited);
        Assert.Equal(["restore", "settings-updated", "shutdown"], calls);
        Assert.Equal(CancellationToken.None, coordinator.CancellationToken);
    }

    [Fact]
    public async Task Exit_is_blocked_and_warns_when_physical_restore_failed()
    {
        var calls = new List<string>();
        var coordinator = new FakeCoordinator(
            calls,
            new ClockReplacementResult(
                false,
                AppSettings.Default with { ReplaceSystemClock = true },
                "restore failed"));
        var orchestrator = new ClockReplacementExitOrchestrator(
            coordinator,
            () => AppSettings.Default with { ReplaceSystemClock = true },
            _ => calls.Add("settings-updated"),
            message => calls.Add($"warning-{message}"),
            () => calls.Add("shutdown"));

        var exited = await orchestrator.TryExitAsync();

        Assert.False(exited);
        Assert.Equal(["restore", "settings-updated", "warning-restore failed"], calls);
        Assert.DoesNotContain("shutdown", calls);
    }

    private sealed class FakeCoordinator(
        List<string> calls,
        ClockReplacementResult result) : IClockReplacementCoordinator
    {
        public bool IsCompatible => true;
        public string CompatibilityMessage => "compatible";
        public CancellationToken CancellationToken { get; private set; }

        public Task<ClockReplacementResult> SetEnabledAsync(
            bool enabled, AppSettings settings, CancellationToken cancellationToken)
        {
            Assert.False(enabled);
            calls.Add("restore");
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }

        public Task<ClockReplacementResult> RecoverOnStartupAsync(
            AppSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
