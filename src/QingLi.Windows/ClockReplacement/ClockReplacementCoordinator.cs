using QingLi.Core.ClockReplacement;
using QingLi.Core.Settings;

namespace QingLi.Windows.ClockReplacement;

public interface IClockReplacementCompatibility
{
    bool IsCompatible { get; }
    string Message { get; }
}

public interface IClockReplacementCoordinator
{
    bool IsCompatible { get; }
    string CompatibilityMessage { get; }

    Task<ClockReplacementResult> SetEnabledAsync(
        bool enabled, AppSettings settings, CancellationToken cancellationToken);

    Task<ClockReplacementResult> RecoverOnStartupAsync(
        AppSettings settings, CancellationToken cancellationToken);
}

public sealed record ClockReplacementResult(
    bool Succeeded, AppSettings Settings, string? ErrorMessage = null);

public sealed class Windows11TaskbarCompatibility : IClockReplacementCompatibility
{
    private readonly ITaskbarGeometryLocator _locator;

    public Windows11TaskbarCompatibility(ITaskbarGeometryLocator locator) =>
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));

    public bool IsCompatible =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22_000) && _locator.GetPrimary() is not null;

    public string Message => IsCompatible
        ? "可替换 Windows 11 任务栏时钟"
        : "仅支持 Windows 11 的底部任务栏；当前将继续使用托盘模式";
}

public sealed class ClockReplacementCoordinator : IClockReplacementCoordinator
{
    private static readonly SystemClockState RestoredStateMarker =
        new(false, null, DateTimeOffset.MinValue);

    private readonly IClockReplacementCompatibility _compatibility;
    private readonly ISystemClockPolicy _policy;
    private readonly ISystemClockStateStore _stateStore;
    private readonly IClockWindowController _windowController;
    private readonly ISettingsStore _settingsStore;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ClockReplacementCoordinator(
        IClockReplacementCompatibility compatibility,
        ISystemClockPolicy policy,
        ISystemClockStateStore stateStore,
        IClockWindowController windowController,
        ISettingsStore settingsStore)
    {
        _compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _windowController = windowController ?? throw new ArgumentNullException(nameof(windowController));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    public bool IsCompatible => _compatibility.IsCompatible;
    public string CompatibilityMessage => _compatibility.Message;

    public async Task<ClockReplacementResult> SetEnabledAsync(
        bool enabled, AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return enabled
                ? await EnableCoreAsync(settings, cancellationToken)
                : await DisableCoreAsync(settings, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ClockReplacementResult> RecoverOnStartupAsync(
        AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            SystemClockState? snapshot;
            try
            {
                snapshot = await _stateStore.LoadAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return await EmergencyShowSystemClockAsync(
                    settings, $"恢复快照损坏或无法读取：{exception.Message}");
            }

            if (snapshot == RestoredStateMarker)
            {
                return await EmergencyShowSystemClockAsync(
                    settings, "检测到上次恢复后的安全标记");
            }

            if (!settings.ReplaceSystemClock)
            {
                return snapshot is null
                    ? new ClockReplacementResult(true, settings)
                    : await RestoreKnownStateAsync(snapshot, settings, persistDisabled: false);
            }

            if (snapshot is null)
            {
                return await EmergencyShowSystemClockAsync(settings, "缺少系统时钟恢复快照");
            }

            if (!IsCompatible)
            {
                return await RestoreKnownStateAsync(
                    snapshot, settings, persistDisabled: true,
                    "当前任务栏不兼容，已恢复系统时钟");
            }

            try
            {
                if (!await _windowController.ShowAsync(cancellationToken))
                {
                    return await RestoreKnownStateAsync(
                        snapshot, settings, persistDisabled: true,
                        "自定义任务栏时钟无法显示，已恢复系统时钟");
                }

                await _policy.HideAsync(cancellationToken);
                return new ClockReplacementResult(true, settings);
            }
            catch (Exception exception)
            {
                var rollback = await RestoreKnownStateAsync(
                    snapshot, settings, persistDisabled: true,
                    $"任务栏时钟恢复启动失败：{exception.Message}");
                if (exception is OperationCanceledException && !rollback.Settings.ReplaceSystemClock)
                {
                    throw;
                }

                return rollback;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ClockReplacementResult> EnableCoreAsync(
        AppSettings settings, CancellationToken cancellationToken)
    {
        if (!IsCompatible)
        {
            return new ClockReplacementResult(
                false, settings with { ReplaceSystemClock = false }, CompatibilityMessage);
        }

        SystemClockState? snapshot = null;
        var snapshotPersisted = false;
        try
        {
            snapshot = await _policy.CaptureAsync(cancellationToken);
            await _stateStore.SaveAsync(snapshot, cancellationToken);
            snapshotPersisted = true;

            if (!await _windowController.ShowAsync(cancellationToken))
            {
                throw new InvalidOperationException("无法定位或显示自定义任务栏时钟");
            }

            await _policy.HideAsync(cancellationToken);
            var enabled = settings with { ReplaceSystemClock = true };
            await _settingsStore.SaveAsync(enabled, cancellationToken);
            return new ClockReplacementResult(true, enabled);
        }
        catch (Exception exception)
        {
            if (!snapshotPersisted || snapshot is null)
            {
                if (exception is OperationCanceledException)
                {
                    throw;
                }

                return Failure(
                    settings with { ReplaceSystemClock = false },
                    "启用任务栏时钟失败", exception);
            }

            var rollback = await RestoreKnownStateAsync(
                snapshot, settings, persistDisabled: true,
                $"启用任务栏时钟失败：{exception.Message}");
            if (exception is OperationCanceledException && !rollback.Settings.ReplaceSystemClock)
            {
                throw;
            }

            return rollback;
        }
    }

    private async Task<ClockReplacementResult> DisableCoreAsync(
        AppSettings settings, CancellationToken cancellationToken)
    {
        SystemClockState? snapshot;
        try
        {
            snapshot = await _stateStore.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await EmergencyShowSystemClockAsync(
                settings, $"恢复快照损坏或无法读取：{exception.Message}");
        }


        if (snapshot == RestoredStateMarker)
        {
            return await EmergencyShowSystemClockAsync(
                settings, "检测到上次恢复后的安全标记");
        }

        return snapshot is null
            ? await EmergencyShowSystemClockAsync(settings, "缺少系统时钟恢复快照")
            : await RestoreKnownStateAsync(snapshot, settings, persistDisabled: true);
    }

    private async Task<ClockReplacementResult> RestoreKnownStateAsync(
        SystemClockState snapshot,
        AppSettings settings,
        bool persistDisabled,
        string? operationMessage = null)
    {
        try
        {
            await _policy.RestoreAsync(snapshot, CancellationToken.None);
        }
        catch (Exception exception)
        {
            return Failure(
                settings with { ReplaceSystemClock = true },
                "恢复系统时钟失败；自定义时钟与恢复快照均已保留",
                exception);
        }

        return await CompletePhysicalRestoreAsync(
            settings, persistDisabled, operationMessage, deleteSnapshot: true);
    }

    private async Task<ClockReplacementResult> EmergencyShowSystemClockAsync(
        AppSettings settings, string reason)
    {
        var emergencyVisibleState = new SystemClockState(
            false, null, DateTimeOffset.UtcNow);
        try
        {
            await _policy.RestoreAsync(emergencyVisibleState, CancellationToken.None);
        }
        catch (Exception exception)
        {
            try
            {
                await _windowController.ShowAsync(CancellationToken.None);
            }
            catch
            {
                // Keep the window untouched if it already exists; report manual recovery below.
            }

            return Failure(
                settings with { ReplaceSystemClock = true },
                $"{reason}，且紧急恢复系统时钟失败；请保留轻历运行并手动恢复",
                exception);
        }

        return await CompletePhysicalRestoreAsync(
            settings,
            persistDisabled: true,
            $"{reason}；已使用紧急策略恢复系统时钟，原策略无法自动还原",
            deleteSnapshot: true);
    }

    private async Task<ClockReplacementResult> CompletePhysicalRestoreAsync(
        AppSettings settings,
        bool persistDisabled,
        string? operationMessage,
        bool deleteSnapshot)
    {
        var disabled = settings with { ReplaceSystemClock = false };
        var errors = new List<string>();
        var settingsSaved = !persistDisabled;
        var snapshotDeleted = !deleteSnapshot;
        var restoredMarkerSaved = false;

        try
        {
            _windowController.Hide();
        }
        catch (Exception exception)
        {
            errors.Add($"隐藏自定义时钟失败：{exception.Message}");
        }

        if (persistDisabled)
        {
            try
            {
                await _settingsStore.SaveAsync(disabled, CancellationToken.None);
                settingsSaved = true;
            }
            catch (Exception exception)
            {
                errors.Add($"保存禁用状态失败：{exception.Message}");
            }
        }

        if (deleteSnapshot)
        {
            try
            {
                await _stateStore.DeleteAsync(CancellationToken.None);
                snapshotDeleted = true;
            }
            catch (Exception exception)
            {
                errors.Add($"清理恢复快照失败：{exception.Message}");
            }
        }

        if (!settingsSaved && !snapshotDeleted)
        {
            try
            {
                await _stateStore.SaveAsync(RestoredStateMarker, CancellationToken.None);
                restoredMarkerSaved = true;
            }
            catch (Exception exception)
            {
                errors.Add($"写入恢复安全标记失败：{exception.Message}");
            }
        }

        var durableSafeState = settingsSaved || snapshotDeleted || restoredMarkerSaved;

        var messages = new List<string>();
        if (!string.IsNullOrWhiteSpace(operationMessage))
        {
            messages.Add(operationMessage);
        }

        messages.AddRange(errors);
        return new ClockReplacementResult(
            messages.Count == 0 && durableSafeState,
            durableSafeState ? disabled : settings with { ReplaceSystemClock = true },
            messages.Count == 0 ? null : string.Join("；", messages));
    }

    private static ClockReplacementResult Failure(
        AppSettings settings, string message, Exception exception) =>
        new(false, settings, $"{message}：{exception.Message}");
}

public sealed class ClockReplacementExitOrchestrator
{
    private readonly IClockReplacementCoordinator _coordinator;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly Action<AppSettings> _settingsUpdated;
    private readonly Action<string> _showWarning;
    private readonly Action _shutdown;

    public ClockReplacementExitOrchestrator(
        IClockReplacementCoordinator coordinator,
        Func<AppSettings> settingsProvider,
        Action<AppSettings> settingsUpdated,
        Action<string> showWarning,
        Action shutdown)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _settingsUpdated = settingsUpdated ?? throw new ArgumentNullException(nameof(settingsUpdated));
        _showWarning = showWarning ?? throw new ArgumentNullException(nameof(showWarning));
        _shutdown = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
    }

    public async Task<bool> TryExitAsync()
    {
        ClockReplacementResult result;
        try
        {
            result = await _coordinator.SetEnabledAsync(
                false, _settingsProvider(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _showWarning($"恢复系统时钟时发生异常，轻历将继续运行：{exception.Message}");
            return false;
        }

        _settingsUpdated(result.Settings);

        if (result.Settings.ReplaceSystemClock)
        {
            _showWarning(result.ErrorMessage ?? "系统时钟尚未恢复，轻历将继续运行");
            return false;
        }

        if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _showWarning(result.ErrorMessage);
        }

        _shutdown();
        return true;
    }
}
