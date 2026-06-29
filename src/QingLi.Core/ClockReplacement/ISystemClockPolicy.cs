namespace QingLi.Core.ClockReplacement;

public interface ISystemClockPolicy
{
    Task<SystemClockState> CaptureAsync(CancellationToken cancellationToken);

    Task HideAsync(CancellationToken cancellationToken);

    Task RestoreAsync(SystemClockState state, CancellationToken cancellationToken);
}

public interface ISystemClockStateStore
{
    Task<SystemClockState?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(SystemClockState state, CancellationToken cancellationToken);

    Task DeleteAsync(CancellationToken cancellationToken);
}
