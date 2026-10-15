using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Services;
using SystemMonitor.Windows;
using Xunit;

namespace SystemMonitor.Tests;

/// <summary>
/// Loads the real WPF windows (XAML + bindings + dependency injection) on an STA thread without
/// showing them. This is the closest automated check that the app still starts after the
/// shared-core refactor: a bad resource key, missing converter or unresolved constructor
/// parameter throws here exactly as it would at launch. The single-instance mutex in
/// App.OnStartup is bypassed because the App is created but never Run().
/// </summary>
public class WindowsUiSmokeTests
{
    [Fact]
    public void MainSettingsAndAboutWindows_LoadWithTheRealServiceGraph()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var temp = Path.Combine(Path.GetTempPath(), "MonWinUiSmoke_" + Guid.NewGuid());
            try
            {
                var app = new App();
                app.InitializeComponent(); // merges the theme and style dictionaries from App.xaml

                var services = new ServiceCollection();
                services.AddMonWinCore();
                services.AddWindowsPlatform(Dispatcher.CurrentDispatcher);
                services.AddSingleton<IAppPaths>(new TempPaths(temp));
                services.AddSingleton<MainWindow>();
                services.AddTransient<SettingsWindow>();
                services.AddTransient<AboutWindow>();

                using var provider = services.BuildServiceProvider();
                var main = provider.GetRequiredService<MainWindow>();
                var settings = provider.GetRequiredService<SettingsWindow>();
                var about = provider.GetRequiredService<AboutWindow>();

                Assert.NotNull(main.DataContext);
                Assert.NotNull(settings.DataContext);
                Assert.NotNull(about.DataContext);

                provider.GetRequiredService<ISystemMonitorService>().Dispose();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "UI smoke test timed out");

        Assert.Null(failure);
    }

    private sealed class TempPaths(string root) : IAppPaths
    {
        public string ConfigDirectory => root;
        public string StateDirectory => root;
    }
}
