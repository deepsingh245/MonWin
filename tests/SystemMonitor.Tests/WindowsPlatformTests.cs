using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Services;
using SystemMonitor.ViewModels;
using Xunit;

namespace SystemMonitor.Tests;

public class WindowsPlatformTests
{
    [Fact]
    public void AppPaths_KeepTheOriginalLocalAppDataLocation()
    {
        // Existing installs keep their settings.json here; if this ever changes, a migration
        // is required first (see docs/ARCHITECTURE.md).
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemMonitor");
        var paths = new WindowsAppPaths();

        Assert.Equal(expected, paths.ConfigDirectory);
        Assert.Equal(expected, paths.StateDirectory);
    }

    [Fact]
    public void StartupRegistryValueName_IsUnchanged()
    {
        // Renaming the Run value would leave a stale autostart entry on every upgraded PC.
        Assert.Equal("MonWinSystemMonitor", StartupService.ValueName);
    }

    [Fact]
    public void Composition_ResolvesEveryServiceTheAppNeeds()
    {
        var services = new ServiceCollection();
        // Point settings/logs at a temp dir so the test never touches the real profile.
        var temp = Path.Combine(Path.GetTempPath(), "MonWinPlatformTest_" + Guid.NewGuid());
        services.AddMonWinCore();
        services.AddWindowsPlatform(Dispatcher.CurrentDispatcher);
        services.AddSingleton<IAppPaths>(new TempPaths(temp));

        try
        {
            using var provider = services.BuildServiceProvider(validateScopes: true);

            Assert.NotNull(provider.GetRequiredService<ICpuMonitor>());
            Assert.NotNull(provider.GetRequiredService<IMemoryMonitor>());
            Assert.NotNull(provider.GetRequiredService<IGpuMonitor>());
            Assert.NotNull(provider.GetRequiredService<IDiskMonitor>());
            Assert.NotNull(provider.GetRequiredService<INetworkMonitor>());
            Assert.NotNull(provider.GetRequiredService<IStartupService>());
            Assert.NotNull(provider.GetRequiredService<ITaskbarService>());
            Assert.NotNull(provider.GetRequiredService<IThemeService>());
            Assert.NotNull(provider.GetRequiredService<IHotkeyService>());
            Assert.NotNull(provider.GetRequiredService<SettingsViewModel>());
            Assert.NotNull(provider.GetRequiredService<MainViewModel>());
            provider.GetRequiredService<ISystemMonitorService>().Dispose();
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
        }
    }

    private sealed class TempPaths(string root) : IAppPaths
    {
        public string ConfigDirectory => root;
        public string StateDirectory => root;
    }
}
