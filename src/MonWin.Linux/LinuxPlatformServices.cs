using Microsoft.Extensions.DependencyInjection;
using MonWin.Linux.Desktop;
using MonWin.Linux.Providers;
using SystemMonitor.Services;

namespace MonWin.Linux;

/// <summary>
/// The Linux half of dependency injection: every Linux implementation of the interfaces
/// declared in MonWin.Core. Shared registrations live in
/// <see cref="SystemMonitor.CoreServiceCollectionExtensions.AddMonWinCore"/>. The OS is chosen
/// here (by which project is running), never by an "if Linux" check in shared code.
/// </summary>
internal static class LinuxPlatformServices
{
    /// <param name="paths">Injected so tests can point everything at a temp directory.</param>
    /// <param name="files">Injected so tests can read fixture files instead of the real /proc and /sys.</param>
    public static IServiceCollection AddLinuxPlatform(this IServiceCollection services, LinuxAppPaths paths, LinuxFiles files)
    {
        services.AddSingleton<IAppPaths>(paths);
        services.AddSingleton(paths);
        services.AddSingleton(files);
        services.AddSingleton<ICpuMonitor, LinuxCpuMonitor>();
        services.AddSingleton<IMemoryMonitor, LinuxMemoryMonitor>();
        services.AddSingleton<IDiskDataSource>(_ => new LinuxDiskDataSource(files));
        services.AddSingleton<IGpuMonitor>(sp => LinuxGpuMonitor.Discover(files, sp.GetRequiredService<ILoggingService>()));
        services.AddSingleton<IStartupService>(sp =>
            new LinuxStartupService(paths.AutostartDirectory, sp.GetRequiredService<ILoggingService>()));
        services.AddSingleton<ITaskbarService, LinuxPanelService>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddSingleton<IHotkeyService, UnsupportedHotkeyService>();
        services.AddSingleton<IColorPickerService, NoColorPickerService>();
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        return services;
    }
}
