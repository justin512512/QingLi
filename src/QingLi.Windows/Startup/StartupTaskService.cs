using Microsoft.Win32;
using System.IO;

namespace QingLi.Windows.Startup;

public interface IStartupTaskService
{
    bool IsEnabled(string executablePath);

    void SetEnabled(bool enabled, string executablePath);
}

public sealed class StartupTaskService : IStartupTaskService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QingLi";

    public bool IsEnabled(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return string.Equals(
            key?.GetValue(ValueName) as string,
            Quote(executablePath),
            StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key.SetValue(ValueName, Quote(executablePath), RegistryValueKind.String);
            return;
        }

        using var existing = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        existing?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string Quote(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return $"\"{Path.GetFullPath(path)}\"";
    }
}
