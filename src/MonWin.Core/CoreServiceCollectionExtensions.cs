using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Services;
using SystemMonitor.ViewModels;

namespace SystemMonitor;

/// <summary>
/// Registers everything that is the same on every OS. Each platform app then adds its own
/// implementations of the platform interfaces (CPU/RAM/GPU/disk providers, startup, theme,
/// taskbar, paths, dispatcher, hotkey, color picker) — that is the only place the OS is
/// chosen, so shared logic never needs an "if Windows / if Linux" check.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddMonWinCore(this IServiceCollection services)
    {
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INetworkMonitor, NetworkMonitorService>();
        services.AddSingleton<IDiskMonitor, DiskMonitorService>();
        services.AddSingleton<ISystemMonitorService, SystemMonitorService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        return services;
    }
}
