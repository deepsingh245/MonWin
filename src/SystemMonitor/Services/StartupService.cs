using System.IO;
using Microsoft.Win32;

namespace SystemMonitor.Services;

/// <summary>
/// Registers/unregisters "start with Windows" via the standard per-user Run key —
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run. No admin rights, no scheduled
/// task, no shortcut/COM plumbing; instantly toggleable and fully reversible.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MonWinSystemMonitor";

    private readonly ILoggingService _logger;
    private readonly Func<string> _executablePathProvider;

    public StartupService(ILoggingService logger) : this(logger, GetDefaultExecutablePath())
    {
    }

    internal StartupService(ILoggingService logger, string executablePath)
    {
        _logger = logger;
        _executablePathProvider = () => executablePath;
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to read startup registry value: {ex.Message}");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                key.SetValue(ValueName, $"\"{_executablePathProvider()}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to update startup registry value", ex);
        }
    }

    private static string GetDefaultExecutablePath() =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SystemMonitor.exe");
}
