using System.Windows;
using System.Windows.Media;
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
    private SettingsWindow? _settingsWindow;
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
        _services.GetRequiredService<ISettingsService>().SettingsChanged += (_, _) => Dispatcher.Invoke(ApplyTheme);

        _mainWindow = _services.GetRequiredService<MainWindow>();

        // Create the HWND now even if the overlay starts hidden, so the global hotkey
        // (registered in MainWindow.OnSourceInitialized) works from the moment we launch.
        new System.Windows.Interop.WindowInteropHelper(_mainWindow).EnsureHandle();

        var settings = _services.GetRequiredService<ISettingsService>().Current;
        // Only the user's "Start minimized" setting hides the overlay. The old startup
        // registry entry passed --minimized, which is deliberately ignored now so the
        // compact view shows after boot by default.
        if (!settings.StartMinimized)
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
        services.AddMonWinCore();
        services.AddWindowsPlatform(Dispatcher.CurrentDispatcher);
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
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

        ApplyAccentOverride(settings.AccentColorHex);
    }

    /// <summary>
    /// Overrides the theme's default accent brushes with a user-picked color. Setting the
    /// keys directly on Application.Resources (rather than inside a merged dictionary)
    /// takes precedence over whatever Light.xaml/Dark.xaml defined, and clearing them
    /// (null/empty hex) falls back to the theme default automatically.
    /// </summary>
    private void ApplyAccentOverride(string? accentHex)
    {
        Resources.Remove("MonWin.Accent");
        Resources.Remove("MonWin.AccentFill");

        if (string.IsNullOrWhiteSpace(accentHex))
        {
            return;
        }

        Color color;
        try
        {
            if (ColorConverter.ConvertFromString(accentHex) is not Color parsed)
            {
                return;
            }

            color = parsed;
        }
        catch (FormatException)
        {
            _logger?.LogWarning($"Invalid accent color '{accentHex}', ignoring.");
            return;
        }

        var accentBrush = new SolidColorBrush(color);
        accentBrush.Freeze();
        Resources["MonWin.Accent"] = accentBrush;

        var fillBrush = new SolidColorBrush(Color.FromArgb(38, color.R, color.G, color.B));
        fillBrush.Freeze();
        Resources["MonWin.AccentFill"] = fillBrush;
    }

    private void SetupTrayIcon()
    {
        var settingsService = _services!.GetRequiredService<ISettingsService>();
        var startupService = _services!.GetRequiredService<IStartupService>();
        var monitor = _services!.GetRequiredService<ISystemMonitorService>();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Monitor", null, (_, _) => Dispatcher.Invoke(() => _mainWindow!.ShowDetailed()));
        menu.Items.Add("Show Compact View", null, (_, _) => Dispatcher.Invoke(() => _mainWindow!.ShowOverlay()));
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

        _defaultTrayIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "SystemMonitor.exe");
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _defaultTrayIcon,
            Visible = true,
            Text = DefaultTrayText,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => _mainWindow!.ShowDetailed());

        monitor.SnapshotUpdated += (_, snapshot) => Dispatcher.BeginInvoke(() => UpdateTrayIcon(snapshot));
        settingsService.SettingsChanged += (_, settings) => Dispatcher.BeginInvoke(() =>
        {
            if (!settings.TrayShowsCpu && _trayIcon is not null)
            {
                _trayIcon.Icon = _defaultTrayIcon;
                _trayRenderer.Dispose();
            }
        });
    }

    private const string DefaultTrayText = "MonWin System Monitor";
    private System.Drawing.Icon? _defaultTrayIcon;
    private readonly TrayIconRenderer _trayRenderer = new();
    private DateTime _lastTrayUpdate = DateTime.MinValue;

    /// <summary>
    /// Keeps the tray tooltip current and, when enabled, redraws the live CPU % into the
    /// icon. Throttled to once a second — at a 250 ms poll interval, re-rendering the icon
    /// every tick is wasted GDI work the eye can't follow anyway.
    /// </summary>
    private void UpdateTrayIcon(Models.MonitorSnapshot snapshot)
    {
        if (_trayIcon is null || DateTime.UtcNow - _lastTrayUpdate < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastTrayUpdate = DateTime.UtcNow;
        var cpu = snapshot.Cpu.IsAvailable ? (int)Math.Round(snapshot.Cpu.UsagePercent) : (int?)null;
        var ram = snapshot.Memory.IsAvailable ? (int)Math.Round(snapshot.Memory.UsagePercent) : (int?)null;
        _trayIcon.Text = cpu is null && ram is null
            ? DefaultTrayText
            : $"MonWin — CPU {cpu?.ToString() ?? "N/A"}% · RAM {ram?.ToString() ?? "N/A"}%";

        var settings = _services!.GetRequiredService<ISettingsService>().Current;
        if (settings.TrayShowsCpu && cpu is { } value)
        {
            _trayIcon.Icon = _trayRenderer.Render(value);
        }
    }

    internal void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        // Non-modal + owned: closes on click-outside (see SettingsWindow.OnDeactivated)
        // like a Windows 11 flyout, rather than blocking the app until a button is clicked.
        _mainWindow?.SetTopmostReassertionSuspended(true);
        _settingsWindow = _services!.GetRequiredService<SettingsWindow>();
        _settingsWindow.Owner = _mainWindow;
        _settingsWindow.Closed += (_, _) => _mainWindow?.SetTopmostReassertionSuspended(false);
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    internal void OpenAbout()
    {
        _mainWindow?.SetTopmostReassertionSuspended(true);
        var window = _services!.GetRequiredService<AboutWindow>();
        window.Owner = _mainWindow;
        window.Closed += (_, _) => _mainWindow?.SetTopmostReassertionSuspended(false);
        window.ShowDialog();
    }

    internal void ExitApplication()
    {
        _logger?.LogInfo("Application exiting.");
        _trayIcon?.Dispose();
        _trayRenderer.Dispose();
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
