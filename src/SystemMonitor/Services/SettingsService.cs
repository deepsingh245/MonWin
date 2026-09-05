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
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Settings file corrupted or unreadable, restoring defaults: {ex.Message}");
            return new AppSettings();
        }
    }
}
