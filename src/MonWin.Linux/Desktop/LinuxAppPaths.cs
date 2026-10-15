using SystemMonitor.Services;

namespace MonWin.Linux.Desktop;

/// <summary>
/// XDG Base Directory locations: settings in $XDG_CONFIG_HOME/monwin (default ~/.config/monwin),
/// logs and state in $XDG_STATE_HOME/monwin (default ~/.local/state/monwin). Per the spec, an
/// XDG variable that is empty or not an absolute path is ignored.
/// </summary>
internal sealed class LinuxAppPaths(Func<string, string?> env, string home) : IAppPaths
{
    public const string AppFolder = "monwin";

    /// <summary>$XDG_CONFIG_HOME itself (autostart entries live in its "autostart" subfolder, not in the app folder).</summary>
    public string ConfigHome => Resolve("XDG_CONFIG_HOME", ".config");

    public string ConfigDirectory => Path.Combine(ConfigHome, AppFolder);

    public string StateDirectory => Path.Combine(Resolve("XDG_STATE_HOME", Path.Combine(".local", "state")), AppFolder);

    public string AutostartDirectory => Path.Combine(ConfigHome, "autostart");

    /// <summary>Preferred place for the single-instance lock: $XDG_RUNTIME_DIR, else the state directory.</summary>
    public string RuntimeDirectory =>
        env("XDG_RUNTIME_DIR") is { Length: > 0 } runtime && Path.IsPathRooted(runtime) ? runtime : StateDirectory;

    public static LinuxAppPaths FromEnvironment() =>
        new(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private string Resolve(string variable, string fallbackRelativeToHome) =>
        env(variable) is { Length: > 0 } value && Path.IsPathRooted(value)
            ? value
            : Path.Combine(home, fallbackRelativeToHome);
}
