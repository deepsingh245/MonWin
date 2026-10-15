using MonWin.Linux.Desktop;
using SystemMonitor.Models;
using SystemMonitor.Services;
using SystemMonitor.Tests;
using Xunit;

namespace MonWin.Linux.Tests;

public class DesktopTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Key, v => v.Value);
        return key => map.TryGetValue(key, out var value) ? value : null;
    }

    // --- session detection -------------------------------------------------------------

    [Theory]
    [InlineData("wayland", "Wayland")]
    [InlineData("WAYLAND", "Wayland")]
    [InlineData("x11", "X11")]
    [InlineData("tty", "Unknown")]
    public void Session_UsesXdgSessionType(string type, string expected)
    {
        // SessionKind is internal, so the theory passes its name (xUnit test methods must be public).
        Assert.Equal(Enum.Parse<SessionKind>(expected), DesktopSession.Detect(Env(("XDG_SESSION_TYPE", type))).Kind);
    }

    [Fact]
    public void Session_FallsBackToDisplayVariables()
    {
        Assert.Equal(SessionKind.Wayland, DesktopSession.Detect(Env(("WAYLAND_DISPLAY", "wayland-0"), ("DISPLAY", ":0"))).Kind);
        Assert.Equal(SessionKind.X11, DesktopSession.Detect(Env(("DISPLAY", ":0"))).Kind);
        Assert.Equal(SessionKind.Unknown, DesktopSession.Detect(Env()).Kind);
    }

    [Fact]
    public void Session_DescribesLimitsHonestly()
    {
        var wayland = DesktopSession.Detect(Env(("XDG_SESSION_TYPE", "wayland"), ("XDG_CURRENT_DESKTOP", "GNOME")));

        Assert.Equal("GNOME", wayland.DesktopEnvironment);
        Assert.Contains("XWayland", wayland.Describe());
        Assert.Contains("may ignore", wayland.Describe());
    }

    // --- XDG paths ---------------------------------------------------------------------

    [Fact]
    public void Paths_DefaultToHomeSubdirectories()
    {
        var home = Path.Combine(Path.GetTempPath(), "home");
        var paths = new LinuxAppPaths(Env(), home);

        Assert.Equal(Path.Combine(home, ".config", "monwin"), paths.ConfigDirectory);
        Assert.Equal(Path.Combine(home, ".local", "state", "monwin"), paths.StateDirectory);
        Assert.Equal(Path.Combine(home, ".config", "autostart"), paths.AutostartDirectory);
    }

    [Fact]
    public void Paths_HonorAbsoluteXdgVariables()
    {
        var config = Path.Combine(Path.GetTempPath(), "cfg");
        var state = Path.Combine(Path.GetTempPath(), "st");
        var paths = new LinuxAppPaths(Env(("XDG_CONFIG_HOME", config), ("XDG_STATE_HOME", state)), "/home/u");

        Assert.Equal(Path.Combine(config, "monwin"), paths.ConfigDirectory);
        Assert.Equal(Path.Combine(state, "monwin"), paths.StateDirectory);
        Assert.Equal(Path.Combine(config, "autostart"), paths.AutostartDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/path")]
    public void Paths_IgnoreEmptyOrRelativeXdgVariables(string value)
    {
        // The XDG spec says relative values must be ignored.
        var home = Path.Combine(Path.GetTempPath(), "home");
        var paths = new LinuxAppPaths(Env(("XDG_CONFIG_HOME", value)), home);

        Assert.Equal(Path.Combine(home, ".config", "monwin"), paths.ConfigDirectory);
    }

    [Fact]
    public void Paths_RuntimeDirectoryFallsBackToState()
    {
        var home = Path.Combine(Path.GetTempPath(), "home");
        var runtime = Path.Combine(Path.GetTempPath(), "run");

        Assert.Equal(runtime, new LinuxAppPaths(Env(("XDG_RUNTIME_DIR", runtime)), home).RuntimeDirectory);
        var fallback = new LinuxAppPaths(Env(), home);
        Assert.Equal(fallback.StateDirectory, fallback.RuntimeDirectory);
    }

    // --- autostart ---------------------------------------------------------------------

    [Fact]
    public void Autostart_FileContainsStandardKeysAndQuotedExec()
    {
        var text = AutostartEntry.Build(["/opt/Mon Win/monwin"]);

        Assert.StartsWith("[Desktop Entry]\n", text);
        Assert.Contains("Type=Application\n", text);
        Assert.Contains("Name=MonWin\n", text);
        Assert.Contains("Exec=\"/opt/Mon Win/monwin\"\n", text);
        Assert.Contains("Terminal=false\n", text);
        Assert.DoesNotContain("\r", text);
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("cost$5", "\"cost\\$5\"")]
    [InlineData("back`tick", "\"back\\`tick\"")]
    [InlineData("100%", "\"100%%\"")]
    [InlineData("C\\dir", "\"C\\\\\\\\dir\"")] // a literal backslash needs four backslashes
    public void Autostart_ExecQuoting_FollowsTheDesktopEntrySpec(string argument, string expected)
    {
        Assert.Equal(expected, AutostartEntry.QuoteExecArgument(argument));
    }

    [Fact]
    public void Autostart_DotnetHostStyleCommandKeepsAllArguments()
    {
        var text = AutostartEntry.Build(["/usr/bin/dotnet", "/home/u/MonWin.Linux.dll"]);

        Assert.Contains("Exec=\"/usr/bin/dotnet\" \"/home/u/MonWin.Linux.dll\"\n", text);
    }

    [Fact]
    public void StartupService_EnableDisableRoundTrip()
    {
        using var root = new FixtureRoot();
        var dir = Path.Combine(root.Path, "autostart");
        var service = new LinuxStartupService(dir, new NoOpLoggingService(), () => ["/opt/monwin"]);

        Assert.False(service.IsEnabled());

        service.SetEnabled(true);
        service.SetEnabled(true); // idempotent
        Assert.True(service.IsEnabled());
        Assert.Contains("Exec=\"/opt/monwin\"", File.ReadAllText(Path.Combine(dir, "monwin.desktop")));
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));

        service.SetEnabled(false);
        service.SetEnabled(false); // disabling when already off is fine
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void StartupService_UnwritableLocation_DoesNotThrow()
    {
        using var root = new FixtureRoot();
        var blocker = Path.Combine(root.Path, "file-not-dir");
        File.WriteAllText(blocker, "x");
        var service = new LinuxStartupService(Path.Combine(blocker, "autostart"), new NoOpLoggingService(), () => ["/opt/monwin"]);

        var exception = Record.Exception(() => service.SetEnabled(true));

        Assert.Null(exception);
        Assert.False(service.IsEnabled());
    }

    // --- single instance ---------------------------------------------------------------

    [Fact]
    public void SingleInstance_SecondAcquireFails_UntilFirstIsReleased()
    {
        using var root = new FixtureRoot();

        var first = SingleInstanceLock.TryAcquire(root.Path);
        Assert.NotNull(first);
        Assert.Null(SingleInstanceLock.TryAcquire(root.Path));

        first!.Dispose();
        using var again = SingleInstanceLock.TryAcquire(root.Path);
        Assert.NotNull(again);
    }

    // --- panel / positioning -----------------------------------------------------------

    [Fact]
    public void PanelService_ReportsNoTaskbarAndFloatsAlongTheWorkArea()
    {
        var service = new LinuxPanelService();
        var work = new ScreenRect(0, 28, 1920, 1080); // e.g. a 28px top panel already excluded

        var info = service.GetTaskbarInfo();
        var (x, y) = service.ComputeOverlayPosition(info, work, OverlayPosition.Right, 0, 0, 0, 0, 400, 30);

        Assert.False(info.Found);
        Assert.Equal((1920 - 400 - 16, 1080 - 30 - 16), (x, y));
    }

    // --- small services ----------------------------------------------------------------

    [Fact]
    public void UnsupportedServices_AreInertAndExplainThemselves()
    {
        var hotkey = new UnsupportedHotkeyService();
        hotkey.Apply("Ctrl+Alt+M");

        Assert.NotNull(hotkey.Status);
        Assert.False(hotkey.IsHotkeyMessage(0x0312, 0));
        Assert.Null(new NoColorPickerService().PickColor("#112233"));
    }

    [Fact]
    public void Theme_ExplicitChoicesDoNotNeedARunningApplication()
    {
        var theme = new AvaloniaThemeService();

        Assert.Equal(EffectiveTheme.Dark, theme.Resolve(AppTheme.Dark));
        Assert.Equal(EffectiveTheme.Light, theme.Resolve(AppTheme.Light));
        // "System" with no desktop preference available falls back to light, like the Windows service.
        Assert.Equal(EffectiveTheme.Light, theme.Resolve(AppTheme.System));
    }
}
