using System.IO;
using SystemMonitor.Services;

namespace SystemMonitor.Services;

/// <summary>
/// Keeps the original Windows location, %LOCALAPPDATA%\SystemMonitor, for both settings
/// and logs. The folder name is a legacy of the project's early name and must not change:
/// renaming it would orphan every existing install's settings.json.
/// </summary>
public sealed class WindowsAppPaths : IAppPaths
{
    public const string FolderName = "SystemMonitor";

    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

    public string ConfigDirectory => _root;

    public string StateDirectory => _root;
}
