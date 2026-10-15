using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Services;

namespace SystemMonitor;

/// <summary>
/// The Windows half of dependency injection: every OS-specific implementation of the
/// interfaces declared in MonWin.Core. Shared registrations live in
/// <see cref="CoreServiceCollectionExtensions.AddMonWinCore"/>.
/// </summary>
public static class WindowsPlatformServices
{
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services, Dispatcher uiDispatcher)
    {
        services.AddSingleton<IAppPaths, WindowsAppPaths>();
        services.AddSingleton<ICpuMonitor, CpuMonitorService>();
        services.AddSingleton<IMemoryMonitor, MemoryMonitorService>();
        services.AddSingleton<IGpuDataSource, WindowsGpuDataSource>();
        services.AddSingleton<IGpuMonitor, GpuMonitorService>();
        services.AddSingleton<IDiskDataSource, WindowsDiskDataSource>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ITaskbarService, TaskbarService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IColorPickerService, WinFormsColorPicker>();
        services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(uiDispatcher));
        return services;
    }
}
