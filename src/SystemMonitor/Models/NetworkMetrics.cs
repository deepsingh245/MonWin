namespace SystemMonitor.Models;

/// <summary>
/// Placeholder model reserved for a future network throughput provider.
/// Not wired into monitoring in v1 (see Settings &gt; Display &gt; Metrics), but the
/// shape exists so <see cref="SystemMonitorService"/> and the UI can adopt it without
/// a breaking change later.
/// </summary>
public sealed record NetworkMetrics
{
    public static readonly NetworkMetrics Unavailable = new() { IsAvailable = false };

    public bool IsAvailable { get; init; } = true;
    public double UploadBytesPerSecond { get; init; }
    public double DownloadBytesPerSecond { get; init; }
}
