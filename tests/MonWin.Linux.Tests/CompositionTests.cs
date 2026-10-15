using Microsoft.Extensions.DependencyInjection;
using MonWin.Linux.Desktop;
using MonWin.Linux.Providers;
using MonWin.Linux.Views;
using SystemMonitor;
using SystemMonitor.Services;
using SystemMonitor.ViewModels;
using Xunit;

namespace MonWin.Linux.Tests;

public class CompositionTests
{
    [Fact]
    public void LinuxPlatform_ResolvesEveryServiceTheAppNeeds()
    {
        using var root = new FixtureRoot();
        root.Write("/proc/stat", "cpu  10 0 10 80 0 0 0 0 0 0\n");
        root.Write("/proc/meminfo", "MemTotal: 1000 kB\nMemAvailable: 500 kB\n");

        // Everything (settings, logs, autostart, /proc, /sys) is redirected into the temp root.
        var home = Path.Combine(root.Path, "home");
        var paths = new LinuxAppPaths(_ => null, home);
        var services = new ServiceCollection();
        services.AddMonWinCore();
        services.AddLinuxPlatform(paths, root.Files);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<LinuxCpuMonitor>(provider.GetRequiredService<ICpuMonitor>());
        Assert.IsType<LinuxMemoryMonitor>(provider.GetRequiredService<IMemoryMonitor>());
        Assert.IsType<LinuxGpuMonitor>(provider.GetRequiredService<IGpuMonitor>());
        Assert.IsType<LinuxStartupService>(provider.GetRequiredService<IStartupService>());
        Assert.IsType<LinuxPanelService>(provider.GetRequiredService<ITaskbarService>());
        Assert.IsType<AvaloniaThemeService>(provider.GetRequiredService<IThemeService>());
        Assert.IsType<UnsupportedHotkeyService>(provider.GetRequiredService<IHotkeyService>());
        Assert.IsType<NoColorPickerService>(provider.GetRequiredService<IColorPickerService>());
        Assert.NotNull(provider.GetRequiredService<IDiskMonitor>());
        Assert.NotNull(provider.GetRequiredService<INetworkMonitor>());
        Assert.NotNull(provider.GetRequiredService<SettingsViewModel>());
        Assert.NotNull(provider.GetRequiredService<AboutViewModel>());

        var main = provider.GetRequiredService<MainViewModel>();
        Assert.NotNull(main);
        Assert.Equal(Path.Combine(home, ".config", "monwin"), paths.ConfigDirectory);

        provider.GetRequiredService<ISystemMonitorService>().Dispose();
    }

    [Fact]
    public void LinuxSettingsAndLogs_LandInTheXdgDirectories()
    {
        using var root = new FixtureRoot();
        var paths = new LinuxAppPaths(_ => null, Path.Combine(root.Path, "home"));
        var services = new ServiceCollection();
        services.AddMonWinCore();
        services.AddLinuxPlatform(paths, root.Files);
        using var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<ISettingsService>();
        var logger = provider.GetRequiredService<ILoggingService>();
        settings.Save(settings.Current.Clone());
        logger.LogInfo("hello");

        Assert.True(File.Exists(Path.Combine(paths.ConfigDirectory, "settings.json")));
        Assert.True(File.Exists(Path.Combine(paths.StateDirectory, "logs", "app.log")));
    }

    [Fact]
    public void Defaults_AreIdenticalToWindows()
    {
        // The shared AppSettings defaults are the single source of truth for both platforms.
        var defaults = new SystemMonitor.Models.AppSettings();

        Assert.True(defaults.ShowCpu && defaults.ShowRam && defaults.ShowGpu);
        Assert.False(defaults.ShowNetwork || defaults.ShowDisk);
        Assert.Equal(500, defaults.UpdateIntervalMs);
    }
}

public class SparklineGeometryTests
{
    [Fact]
    public void FlatData_SitsOnTheBaseline()
    {
        var points = SparklineGeometry.ComputePoints([0, 0, 0], 100, 20, 100);

        Assert.All(points, p => Assert.Equal(20, p.Y));
        Assert.Equal(0, points[0].X);
        Assert.Equal(100, points[^1].X);
    }

    [Fact]
    public void FullScale_ReachesTheTop()
    {
        var points = SparklineGeometry.ComputePoints([0, 100], 50, 10, 100);

        Assert.Equal(10, points[0].Y);
        Assert.Equal(0, points[1].Y);
    }

    [Fact]
    public void AutoScale_UsesTheDataMaximumWhenMaxValueIsZero()
    {
        // Byte rates: MaxValue 0 means "scale to the largest sample".
        var points = SparklineGeometry.ComputePoints([0, 500, 1000], 100, 10, 0);

        Assert.Equal(5, points[1].Y, 3);
        Assert.Equal(0, points[2].Y, 3);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, 0)]
    [InlineData(-1, 10)]
    public void DegenerateSizesAndShortSeriesDrawNothing(double width, double height)
    {
        Assert.Empty(SparklineGeometry.ComputePoints([1, 2, 3], width, height, 100));
        Assert.Empty(SparklineGeometry.ComputePoints([1], 100, 10, 100));
        Assert.Empty(SparklineGeometry.ComputePoints(null, 100, 10, 100));
    }

    [Fact]
    public void NonFiniteValues_AreDrawnAsZero()
    {
        var points = SparklineGeometry.ComputePoints([double.NaN, double.PositiveInfinity, 50], 100, 10, 100);

        Assert.All(points, p => Assert.True(double.IsFinite(p.Y)));
    }
}
