using QingLi.Core.Settings;
using QingLi.Windows.ClockReplacement;

namespace QingLi.Windows.Tests.ClockReplacement;

public sealed class ClockReplacementStartupOrchestratorTests
{
    [Fact]
    public async Task First_run_enables_taskbar_clock_instead_of_recovery()
    {
        var coordinator = new RecordingCoordinator();
        var orchestrator = new ClockReplacementStartupOrchestrator(coordinator);

        var result = await orchestrator.StartAsync(
            isFirstRun: true,
            AppSettings.Default,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Settings.ReplaceSystemClock);
        Assert.Equal(["enable-True"], coordinator.Calls);
    }

    [Fact]
    public async Task Existing_install_uses_recovery_without_reenabling()
    {
        var coordinator = new RecordingCoordinator();
        var orchestrator = new ClockReplacementStartupOrchestrator(coordinator);

        await orchestrator.StartAsync(
            isFirstRun: false,
            AppSettings.Default,
            CancellationToken.None);

        Assert.Equal(["recover"], coordinator.Calls);
    }

    private sealed class RecordingCoordinator : IClockReplacementCoordinator
    {
        public List<string> Calls { get; } = [];

        public bool IsCompatible => true;

        public string CompatibilityMessage => "compatible";

        public Task<ClockReplacementResult> SetEnabledAsync(
            bool enabled,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            Calls.Add($"enable-{enabled}");
            return Task.FromResult(new ClockReplacementResult(
                true,
                settings with { ReplaceSystemClock = enabled }));
        }

        public Task<ClockReplacementResult> RecoverOnStartupAsync(
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            Calls.Add("recover");
            return Task.FromResult(new ClockReplacementResult(true, settings));
        }
    }
}
