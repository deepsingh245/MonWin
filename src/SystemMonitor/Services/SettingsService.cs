using System.IO;
using System.Text.Json;
using SystemMonitor.Models;

namespace SystemMonitor.Services;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON under %LOCALAPPDATA%\SystemMonitor\.
/// Writes are atomic (temp file + move) and a missing or corrupted file always falls
/// back to defaults rather than crashing the app.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;
    private readonly ILoggingService _logger;

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings Current { get; private set; }

    public SettingsService(ILoggingService logger)
    {
        _logger = logger;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemMonitor");
        Directory.CreateDirectory(dir);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        Current = Load(_settingsFilePath, _logger);
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tempPath = _settingsFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsFilePath, overwrite: true);
            Current = settings;
            SettingsChanged?.Invoke(this, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save settings", ex);
        }
    }

    /// <summary>Extracted for unit testing without touching the real %LOCALAPPDATA% path.</summary>
    internal static AppSettings Load(string path, ILoggingService logger)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Sanitize(settings ?? new AppSettings());
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Settings file corrupted or unreadable, restoring defaults: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Deserializing a valid JSON file doesn't guarantee valid field values — a hand-edited
    /// or otherwise tampered settings.json could contain an out-of-range value that a plain
    /// parse wouldn't catch. In particular, an invalid UpdateIntervalMs (zero/negative) would
    /// crash System.Threading.Timer's constructor on startup, and that crash happens early
    /// enough in App.OnStartup that it leaves no window and no tray icon behind — the app
    /// would look like it silently failed to launch at all. Clamp/replace anything invalid
    /// here so a bad file degrades to defaults for that field, never to a crash.
    /// </summary>
    private static AppSettings Sanitize(AppSettings settings)
    {
        if (!AppSettings.ValidUpdateIntervalsMs.Contains(settings.UpdateIntervalMs))
        {
            settings.UpdateIntervalMs = 500;
        }

        if (!AppSettings.ValidHistorySeconds.Contains(settings.HistorySeconds))
        {
            settings.HistorySeconds = 60;
        }

        settings.OverlayScale = AppSettings.ClampOverlayScale(settings.OverlayScale);

        if (!Enum.IsDefined(settings.DisplayMode))
        {
            settings.DisplayMode = DisplayMode.CompactGraph;
        }

        if (!Enum.IsDefined(settings.Position))
        {
            settings.Position = OverlayPosition.Left;
        }

        if (!Enum.IsDefined(settings.Theme))
        {
            settings.Theme = AppTheme.System;
        }

        return settings;
    }
}
