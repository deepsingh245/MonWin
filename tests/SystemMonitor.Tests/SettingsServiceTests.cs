using System.IO;
using SystemMonitor.Models;
using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILoggingService _logger = new NoOpLoggingService();

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MonWinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "missing.json");
        var settings = SettingsService.Load(path, _logger);

        Assert.Equal(new AppSettings().UpdateIntervalMs, settings.UpdateIntervalMs);
        Assert.Equal(DisplayMode.CompactGraph, settings.DisplayMode);
    }

    [Fact]
    public void Load_CorruptedFile_FallsBackToDefaults()
    {
        var path = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(path, "{ not valid json ][");

        var settings = SettingsService.Load(path, _logger);

        Assert.Equal(new AppSettings().HistorySeconds, settings.HistorySeconds);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var original = new AppSettings
        {
            UpdateIntervalMs = 1000,
            HistorySeconds = 120,
            Position = OverlayPosition.Right,
            Theme = AppTheme.Dark,
            ShowGpu = false,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);

        var loaded = SettingsService.Load(path, _logger);

        Assert.Equal(original.UpdateIntervalMs, loaded.UpdateIntervalMs);
        Assert.Equal(original.HistorySeconds, loaded.HistorySeconds);
        Assert.Equal(original.Position, loaded.Position);
        Assert.Equal(original.Theme, loaded.Theme);
        Assert.False(loaded.ShowGpu);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
