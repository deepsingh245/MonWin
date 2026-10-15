using System.Reflection;
using SystemMonitor.Services;

namespace MonWin.Linux.Desktop;

/// <summary>"Start with Windows" equivalent: an XDG Autostart file in ~/.config/autostart.</summary>
internal sealed class LinuxStartupService : IStartupService
{
    private readonly string _autostartDirectory;
    private readonly Func<IReadOnlyList<string>> _commandProvider;
    private readonly ILoggingService _logger;

    public LinuxStartupService(string autostartDirectory, ILoggingService logger, Func<IReadOnlyList<string>>? commandProvider = null)
    {
        _autostartDirectory = autostartDirectory;
        _logger = logger;
        _commandProvider = commandProvider ?? DefaultCommand;
    }

    private string FilePath => Path.Combine(_autostartDirectory, AutostartEntry.FileName);

    public bool IsEnabled()
    {
        try
        {
            return File.Exists(FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not read autostart state: {ex.Message}");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                Directory.CreateDirectory(_autostartDirectory);
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, AutostartEntry.Build(_commandProvider()));
                File.Move(temp, FilePath, overwrite: true);
            }
            else if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to {(enabled ? "enable" : "disable")} autostart", ex);
        }
    }

    /// <summary>
    /// The command that relaunches this app. A published build runs through its own apphost;
    /// during development it runs as "dotnet MonWin.Linux.dll", which needs the dll argument.
    /// </summary>
    private static IReadOnlyList<string> DefaultCommand()
    {
        var process = Environment.ProcessPath ?? "monwin";
        var isDotnetHost = string.Equals(Path.GetFileNameWithoutExtension(process), "dotnet", StringComparison.OrdinalIgnoreCase);
        var entry = Assembly.GetEntryAssembly()?.Location;
        return isDotnetHost && !string.IsNullOrEmpty(entry) ? [process, entry] : [process];
    }
}
