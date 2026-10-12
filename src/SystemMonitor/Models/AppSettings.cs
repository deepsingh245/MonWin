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

public enum NetworkUnit
{
    /// <summary>KB/s, MB/s (1024 base) — matches file-copy speeds.</summary>
    Bytes,

    /// <summary>Kbps, Mbps (1000 base) — matches how ISPs advertise speeds.</summary>
    Bits,
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

    /// <summary>Per-metric sparkline in Compact+Graph mode; turning one off shows that metric as text only.</summary>
    public bool GraphCpu { get; set; } = true;
    public bool GraphRam { get; set; } = true;
    public bool GraphGpu { get; set; } = true;
    public bool GraphNetwork { get; set; } = true;
    public bool GraphDisk { get; set; } = true;

    public NetworkUnit NetworkUnit { get; set; } = NetworkUnit.Bytes;

    /// <summary>Warning/critical percentages at which a metric's value turns amber/red.</summary>
    public int CpuWarn { get; set; } = DefaultWarnThreshold;
    public int CpuCrit { get; set; } = DefaultCritThreshold;
    public int RamWarn { get; set; } = DefaultWarnThreshold;
    public int RamCrit { get; set; } = DefaultCritThreshold;
    public int GpuWarn { get; set; } = DefaultWarnThreshold;
    public int GpuCrit { get; set; } = DefaultCritThreshold;
    public int DiskWarn { get; set; } = DefaultWarnThreshold;
    public int DiskCrit { get; set; } = DefaultCritThreshold;

    /// <summary>Draw the live CPU percentage into the tray icon instead of the app icon.</summary>
    public bool TrayShowsCpu { get; set; }

    /// <summary>Global show/hide shortcut, e.g. "Ctrl+Alt+M". Empty = disabled.</summary>
    public string ToggleHotkey { get; set; } = DefaultToggleHotkey;

    public OverlayPosition Position { get; set; } = OverlayPosition.Left;
    public int PositionOffsetX { get; set; }
    public int PositionOffsetY { get; set; }
    public int CustomX { get; set; } = 100;
    public int CustomY { get; set; } = 100;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool ShowTooltip { get; set; } = true;
    public bool ClickThrough { get; set; }

    /// <summary>Null = use the current theme's built-in accent color.</summary>
    public string? AccentColorHex { get; set; }

    /// <summary>Uniform scale applied to the whole card (drag the resize grip to change). 1.0 = default size.</summary>
    public double OverlayScale { get; set; } = 1.0;

    /// <summary>Null = auto-select the busiest adapter at first run.</summary>
    public int? SelectedGpuAdapterIndex { get; set; }

    public static readonly int[] ValidUpdateIntervalsMs = [250, 500, 1000, 2000];
    public static readonly int[] ValidHistorySeconds = [30, 60, 120, 300];

    public const double MinOverlayScale = 0.7;
    public const double MaxOverlayScale = 2.0;

    public static double ClampOverlayScale(double scale) => Math.Clamp(scale, MinOverlayScale, MaxOverlayScale);

    public const int DefaultWarnThreshold = 75;
    public const int DefaultCritThreshold = 90;
    public const string DefaultToggleHotkey = "Ctrl+Alt+M";

    /// <summary>
    /// Shallow copy (every member is a value type or immutable string). Edits start from a
    /// copy of the current settings so fields a given UI doesn't expose are never reset.
    /// </summary>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
