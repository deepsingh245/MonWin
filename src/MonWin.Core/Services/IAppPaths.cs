namespace SystemMonitor.Services;

/// <summary>
/// Where an OS keeps MonWin's local files. Everything stays on the machine; nothing here
/// is ever uploaded. Windows keeps its original %LOCALAPPDATA%\SystemMonitor location so
/// existing installs keep their settings; Linux uses the XDG directories.
/// </summary>
public interface IAppPaths
{
    /// <summary>Holds settings.json.</summary>
    string ConfigDirectory { get; }

    /// <summary>Holds logs (under a "logs" subfolder) and other runtime state.</summary>
    string StateDirectory { get; }
}
