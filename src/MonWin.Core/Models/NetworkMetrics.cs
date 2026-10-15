namespace SystemMonitor.Models;

/// <summary>Aggregate network throughput across all active, non-loopback interfaces for a single poll.</summary>
public sealed record NetworkMetrics
{
    public static readonly NetworkMetrics Unavailable = new() { IsAvailable = false };

    public bool IsAvailable { get; init; } = true;
    public double UploadBytesPerSecond { get; init; }
    public double DownloadBytesPerSecond { get; init; }
}
