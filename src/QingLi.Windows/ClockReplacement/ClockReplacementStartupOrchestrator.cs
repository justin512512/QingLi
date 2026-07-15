using QingLi.Core.Settings;

namespace QingLi.Windows.ClockReplacement;

public sealed class ClockReplacementStartupOrchestrator(
    IClockReplacementCoordinator coordinator)
{
    private readonly IClockReplacementCoordinator _coordinator =
        coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    public Task<ClockReplacementResult> StartAsync(
        bool isFirstRun,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return isFirstRun
            ? _coordinator.SetEnabledAsync(true, settings, cancellationToken)
            : _coordinator.RecoverOnStartupAsync(settings, cancellationToken);
    }
}
