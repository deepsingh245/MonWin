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

    [Fact]
    public void Load_OutOfRangeUpdateInterval_FallsBackToValidDefault()
    {
        // A hand-edited or tampered settings.json with e.g. UpdateIntervalMs=0 or negative
        // would otherwise crash System.Threading.Timer's constructor on startup — this must
        // never reach that far.
        var path = Path.Combine(_tempDir, "bad-interval.json");
        File.WriteAllText(path, """{ "UpdateIntervalMs": -50 }""");

        var settings = SettingsService.Load(path, _logger);

        Assert.Contains(settings.UpdateIntervalMs, AppSettings.ValidUpdateIntervalsMs);
    }

    [Fact]
    public void Load_OutOfRangeOverlayScale_ClampsToValidRange()
    {
        var path = Path.Combine(_tempDir, "bad-scale.json");
        File.WriteAllText(path, """{ "OverlayScale": 999.0 }""");

        var settings = SettingsService.Load(path, _logger);

        Assert.InRange(settings.OverlayScale, AppSettings.MinOverlayScale, AppSettings.MaxOverlayScale);
    }

    [Fact]
    public void Load_OutOfRangeEnumValues_FallBackToDefaults()
    {
        var path = Path.Combine(_tempDir, "bad-enums.json");
        File.WriteAllText(path, """{ "DisplayMode": 999, "Position": -1, "Theme": 42 }""");

        var settings = SettingsService.Load(path, _logger);

        Assert.True(Enum.IsDefined(settings.DisplayMode));
        Assert.True(Enum.IsDefined(settings.Position));
        Assert.True(Enum.IsDefined(settings.Theme));
    }

    [Fact]
    public void Load_InvalidThresholds_AreClampedAndOrdered()
    {
        var path = Path.Combine(_tempDir, "bad-thresholds.json");
        File.WriteAllText(path, """{ "CpuWarn": 95, "CpuCrit": 80, "RamWarn": -10, "RamCrit": 500 }""");

        var settings = SettingsService.Load(path, _logger);

        Assert.Equal((80, 80), (settings.CpuWarn, settings.CpuCrit));
        Assert.Equal((1, 100), (settings.RamWarn, settings.RamCrit));
    }

    [Fact]
    public void Load_NullHotkeyAndBadUnit_FallBackSafely()
    {
        var path = Path.Combine(_tempDir, "bad-misc.json");
        File.WriteAllText(path, """{ "ToggleHotkey": null, "NetworkUnit": 7 }""");

        var settings = SettingsService.Load(path, _logger);

        Assert.Equal(string.Empty, settings.ToggleHotkey);
        Assert.Equal(NetworkUnit.Bytes, settings.NetworkUnit);
    }

    [Fact]
    public void Clone_IsIndependentCopy()
    {
        var original = new AppSettings { OverlayScale = 1.5, CpuWarn = 60 };

        var copy = original.Clone();
        copy.CpuWarn = 70;

        Assert.Equal(1.5, copy.OverlayScale);
        Assert.Equal(60, original.CpuWarn);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
