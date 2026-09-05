using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Services;
using SystemMonitor.ViewModels;
using SystemMonitor.Windows;
using Application = System.Windows.Application;

namespace SystemMonitor;

public partial class App : Application
{
    private ServiceProvider? _services;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;
    private ILoggingService? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\MonWin.SystemMonitor.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        _logger = _services.GetRequiredService<ILoggingService>();
        _logger.LogInfo("Application starting.");

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        ApplyTheme();
        _services.GetRequiredService<IThemeService>().SystemThemeChanged += (_, _) => Dispatcher.Invoke(ApplyTheme);

        _mainWindow = _services.GetRequiredService<MainWindow>();

        var settings = _services.GetRequiredService<ISettingsService>().Current;
        var startMinimized = settings.StartMinimized || e.Args.Contains("--minimized");
        if (!startMinimized)
        {
            _mainWindow.ShowOverlay();
        }
        // When starting minimized, the overlay stays hidden — the tray icon (below)
        // still lets the user open it, and monitoring runs regardless of window state.

        SetupTrayIcon();
        _logger.LogInfo("Application started.");
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICpuMonitor, CpuMonitorService>();
        services.AddSingleton<IMemoryMonitor, MemoryMonitorService>();
        services.AddSingleton<IGpuDataSource, WindowsGpuDataSource>();
        services.AddSingleton<IGpuMonitor, GpuMonitorService>();
        services.AddSingleton<ISystemMonitorService, SystemMonitorService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ITaskbarService, TaskbarService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton(_ => Dispatcher.CurrentDispatcher);
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<AboutWindow>();
    }

    private void ApplyTheme()
    {
        var themeService = _services!.GetRequiredService<IThemeService>();
        var settings = _services!.GetRequiredService<ISettingsService>().Current;
        var effective = themeService.Resolve(settings.Theme);

        var dictionaries = Resources.MergedDictionaries;
        var themeSource = effective == EffectiveTheme.Dark ? "Resources/Themes/Dark.xaml" : "Resources/Themes/Light.xaml";

        var existing = dictionaries.FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("Themes/"));
        var newDictionary = new ResourceDictionary { Source = new Uri(themeSource, UriKind.Relative) };

        if (existing is not null)
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = newDictionary;
        }
        else
        {
            dictionaries.Insert(0, newDictionary);
        }
    }

    private void SetupTrayIcon()
    {
        var settingsService = _services!.GetRequiredService<ISettingsService>();
        var startupService = _services!.GetRequiredService<IStartupService>();
        var monitor = _services!.GetRequiredService<ISystemMonitorService>();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Monitor", null, (_, _) => Dispatcher.Invoke(() => _mainWindow!.ShowDetailed()));
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(OpenSettings));

        var pauseItem = new System.Windows.Forms.ToolStripMenuItem("Pause Monitoring") { CheckOnClick = true };
        pauseItem.Click += (_, _) =>
        {
            if (pauseItem.Checked) monitor.Pause(); else monitor.Resume();
        };
        menu.Items.Add(pauseItem);

        var startupItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = startupService.IsEnabled() };
        startupItem.Click += (_, _) =>
        {
            startupService.SetEnabled(startupItem.Checked);
            var s = settingsService.Current;
            s.StartWithWindows = startupItem.Checked;
            settingsService.Save(s);
        };
        menu.Items.Add(startupItem);

        menu.Items.Add("About", null, (_, _) => Dispatcher.Invoke(OpenAbout));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "SystemMonitor.exe"),
            Visible = true,
            Text = "MonWin System Monitor",
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => _mainWindow!.ShowDetailed());
    }

    internal void OpenSettings()
    {
        var window = _services!.GetRequiredService<SettingsWindow>();
        window.Owner = _mainWindow;
        window.ShowDialog();
    }

    internal void OpenAbout()
    {
        var window = _services!.GetRequiredService<AboutWindow>();
        window.Owner = _mainWindow;
        window.ShowDialog();
    }

    internal void ExitApplication()
    {
        _logger?.LogInfo("Application exiting.");
        _trayIcon?.Dispose();
        (_services?.GetService<ISystemMonitorService>())?.Dispose();
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError("Unhandled UI exception", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _services?.Dispose();
        base.OnExit(e);
    }
}
