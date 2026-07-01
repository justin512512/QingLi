using QingLi.Core.ClockReplacement;

namespace QingLi.Windows.ClockReplacement;

public sealed record ClockRecoveryResult(bool Succeeded, string Message);

public sealed class ClockRecoveryService(
    ISystemClockPolicy policy,
    ISystemClockStateStore stateStore)
{
    public async Task<ClockRecoveryResult> RestoreAsync(CancellationToken cancellationToken)
    {
        SystemClockState? snapshot;
        var emergency = false;
        try
        {
            snapshot = await stateStore.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            snapshot = null;
            emergency = true;
        }

        emergency |= snapshot is null;
        var state = snapshot ?? new SystemClockState(false, null, DateTimeOffset.UtcNow);

        try
        {
            await policy.RestoreAsync(state, CancellationToken.None);
            await stateStore.DeleteAsync(CancellationToken.None);
            return new ClockRecoveryResult(
                true,
                emergency
                    ? "系统时钟已紧急恢复；原 HideClock 策略因快照缺失或损坏而无法自动还原。"
                    : "系统时钟已按轻历保存的原始状态恢复。");
        }
        catch (Exception exception)
        {
            return new ClockRecoveryResult(false, $"系统时钟恢复失败：{exception.Message}");
        }
    }
}
