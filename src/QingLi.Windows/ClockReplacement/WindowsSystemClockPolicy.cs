using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using QingLi.Core.ClockReplacement;

namespace QingLi.Windows.ClockReplacement;

public interface IUserRegistry
{
    bool TryGetInt32(string path, string name, out int value);

    void SetInt32(string path, string name, int value);

    void DeleteValue(string path, string name);
}

public interface IShellSettingsBroadcaster
{
    void BroadcastPolicyChanged();
}

public sealed class WindowsSystemClockPolicy : ISystemClockPolicy
{
    public const string PolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    public const string ValueName = "HideClock";

    private readonly IUserRegistry _registry;
    private readonly IShellSettingsBroadcaster _broadcaster;

    public WindowsSystemClockPolicy()
        : this(new WindowsUserRegistry(), new ShellSettingsBroadcaster())
    {
    }

    public WindowsSystemClockPolicy(
        IUserRegistry registry,
        IShellSettingsBroadcaster broadcaster)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
    }

    public Task<SystemClockState> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existed = _registry.TryGetInt32(PolicyPath, ValueName, out var value);
        return Task.FromResult(new SystemClockState(
            existed,
            existed ? value : null,
            DateTimeOffset.UtcNow));
    }

    public Task HideAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _registry.SetInt32(PolicyPath, ValueName, 1);
        _broadcaster.BroadcastPolicyChanged();
        return Task.CompletedTask;
    }

    public Task RestoreAsync(SystemClockState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        if (state.ValueExisted)
        {
            if (state.OriginalValue is not int originalValue)
            {
                throw new InvalidOperationException(
                    "A clock state marked as existing must include its original value.");
            }

            _registry.SetInt32(PolicyPath, ValueName, originalValue);
        }
        else
        {
            _registry.DeleteValue(PolicyPath, ValueName);
        }

        _broadcaster.BroadcastPolicyChanged();
        return Task.CompletedTask;
    }
}

internal sealed class WindowsUserRegistry : IUserRegistry
{
    public bool TryGetInt32(string path, string name, out int value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        var rawValue = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is not int intValue)
        {
            throw new InvalidOperationException(
                $"HKCU\\{path}\\{name} is not a DWORD value and will not be overwritten.");
        }

        value = intValue;
        return true;
    }

    public void SetInt32(string path, string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path, writable: true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    public void DeleteValue(string path, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

internal sealed class ShellSettingsBroadcaster : IShellSettingsBroadcaster
{
    private const uint HwndBroadcast = 0xffff;
    private const uint WmSettingChange = 0x001a;
    private const uint SmtoAbortIfHung = 0x0002;

    public void BroadcastPolicyChanged()
    {
        var result = SendMessageTimeout(
            (nint)HwndBroadcast,
            WmSettingChange,
            0,
            "Policy",
            SmtoAbortIfHung,
            2_000,
            out _);

        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern nint SendMessageTimeout(
        nint windowHandle,
        uint message,
        nuint wParam,
        string lParam,
        uint flags,
        uint timeout,
        out nuint result);
}
