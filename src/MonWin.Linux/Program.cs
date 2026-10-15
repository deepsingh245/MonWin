using Avalonia;
using MonWin.Linux.Desktop;

namespace MonWin.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var paths = LinuxAppPaths.FromEnvironment();

        // One MonWin per user session; a second launch just exits quietly (the first one's
        // overlay and tray icon are already there).
        using var instanceLock = SingleInstanceLock.TryAcquire(paths.RuntimeDirectory);
        if (instanceLock is null)
        {
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia designer/previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
