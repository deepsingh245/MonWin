namespace SystemMonitor.Models;

public enum DisplayMode
{
    Compact,
    CompactGraph,
    Detailed,
}

public enum OverlayPosition
{
    Left,
    Center,
    Right,
    Custom,
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// User-configurable application settings, persisted as JSON under
/// %LOCALAPPDATA%\SystemMonitor\settings.json. Every field has a safe default so a
/// missing or corrupted settings file can always be replaced by <c>new AppSettings()</c>.
/// </summary>
public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public int UpdateIntervalMs { get; set; } = 500;
    public int HistorySeconds { get; set; } = 60;

    public DisplayMode DisplayMode { get; set; } = DisplayMode.CompactGraph;

    public bool ShowCpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowNetwork { get; set; }
    public bool ShowDisk { get; set; }

    public OverlayPosition Position { get; set; } = OverlayPosition.Left;
    public int PositionOffsetX { get; set; }
    public int PositionOffsetY { get; set; }
    public int CustomX { get; set; } = 100;
    public int CustomY { get; set; } = 100;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool ShowTooltip { get; set; } = true;
    public bool ClickThrough { get; set; }

    /// <summary>Null = auto-select the busiest adapter at first run.</summary>
    public int? SelectedGpuAdapterIndex { get; set; }

    public static readonly int[] ValidUpdateIntervalsMs = [250, 500, 1000, 2000];
    public static readonly int[] ValidHistorySeconds = [30, 60, 120, 300];
}
