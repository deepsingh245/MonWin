using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using MonWin.Linux.Desktop;
using MonWin.Linux.Providers;
using MonWin.Linux.Views;
using SystemMonitor;
using SystemMonitor.Models;
using SystemMonitor.Services;
using SystemMonitor.ViewModels;

namespace MonWin.Linux;

public partial class App : Application
{
    private const string DefaultTrayText = "MonWin System Monitor";

    private ServiceProvider? _services;
    private IClassicDesktopStyleApplicationLifetime? _lifetime;
    private ILoggingService? _logger;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIcon? _trayIcon;
    private DateTime _lastTrayUpdate = DateTime.MinValue;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _lifetime = desktop;
            // Closing the overlay or settings must never end the app; only Exit does.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Cleanup();
            Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Start()
    {
        var services = new ServiceCollection();
        services.AddMonWinCore();
        services.AddLinuxPlatform(LinuxAppPaths.FromEnvironment(), new LinuxFiles());
        _services = services.BuildServiceProvider();

        _logger = _services.GetRequiredService<ILoggingService>();
        _logger.LogInfo("Application starting.");
        _logger.LogInfo(DesktopSession.FromEnvironment().Describe());

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            _logger.LogError("Unhandled UI exception", e.Exception);
            e.Handled = true;
        };

        var settingsService = _services.GetRequiredService<ISettingsService>();
        ApplyTheme();
        _services.GetRequiredService<IThemeService>().SystemThemeChanged += (_, _) => Dispatcher.UIThread.Post(ApplyTheme);
        settingsService.SettingsChanged += (_, _) => Dispatcher.UIThread.Post(ApplyTheme);

        _mainWindow = new MainWindow(
            _services.GetRequiredService<MainViewModel>(),
            _services.GetRequiredService<IStartupService>());

        if (!settingsService.Current.StartMinimized)
        {
            _mainWindow.ShowOverlay();
        }

        SetupTray();
        _logger.LogInfo("Application started.");
    }

    private void ApplyTheme()
    {
        var settings = _services!.GetRequiredService<ISettingsService>().Current;
        var theme = _services!.GetRequiredService<IThemeService>().Resolve(settings.Theme);
        ThemePalette.Apply(this, theme, settings.AccentColorHex);
    }

    private void SetupTray()
    {
        var monitor = _services!.GetRequiredService<ISystemMonitorService>();

        var menu = new NativeMenu();
        menu.Items.Add(MenuItem("Open Monitor", () => _mainWindow!.ShowDetailed()));
        menu.Items.Add(MenuItem("Show Compact View", () => _mainWindow!.ShowOverlay()));
        menu.Items.Add(MenuItem("Settings", OpenSettings));

        var pause = new NativeMenuItem("Pause Monitoring") { ToggleType = NativeMenuItemToggleType.CheckBox };
        pause.Click += (_, _) => { if (pause.IsChecked) monitor.Pause(); else monitor.Resume(); };
        menu.Items.Add(pause);

        menu.Items.Add(MenuItem("About", OpenAbout));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(MenuItem("Exit", ExitApplication));

        // The tray needs a StatusNotifierItem host (KDE has one; GNOME needs the AppIndicator
        // extension). Without one the icon is simply absent and the overlay's own right-click
        // menu offers the same actions, so nothing here is required for the app to work.
        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://MonWin.Linux/Assets/monwin.png"))),
            ToolTipText = DefaultTrayText,
            Menu = menu,
        };
        _trayIcon.Clicked += (_, _) => _mainWindow!.ShowDetailed();
        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });

        monitor.SnapshotUpdated += (_, snapshot) => Dispatcher.UIThread.Post(() => UpdateTrayTooltip(snapshot));
    }

    private static NativeMenuItem MenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Keeps the tray tooltip current, at most once per second.</summary>
    private void UpdateTrayTooltip(MonitorSnapshot snapshot)
    {
        if (_trayIcon is null || DateTime.UtcNow - _lastTrayUpdate < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastTrayUpdate = DateTime.UtcNow;
        var cpu = snapshot.Cpu.IsAvailable ? (int)Math.Round(snapshot.Cpu.UsagePercent) : (int?)null;
        var ram = snapshot.Memory.IsAvailable ? (int)Math.Round(snapshot.Memory.UsagePercent) : (int?)null;
        _trayIcon.ToolTipText = cpu is null && ram is null
            ? DefaultTrayText
            : $"MonWin - CPU {cpu?.ToString() ?? "N/A"}% · RAM {ram?.ToString() ?? "N/A"}%";
    }

    internal void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_services!.GetRequiredService<SettingsViewModel>());
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    internal void OpenAbout()
    {
        var window = new AboutWindow(_services!.GetRequiredService<AboutViewModel>());
        window.Show();
        window.Activate();
    }

    internal void ExitApplication()
    {
        _logger?.LogInfo("Application exiting.");
        _lifetime?.Shutdown();
    }

    private void Cleanup()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
        }

        _services?.Dispose();
    }
}
