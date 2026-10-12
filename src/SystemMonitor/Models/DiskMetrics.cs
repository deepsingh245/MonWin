namespace SystemMonitor.Models;

/// <summary>Aggregate physical-disk activity across all drives for a single poll.</summary>
public sealed record DiskMetrics
{
    public static readonly DiskMetrics Unavailable = new() { IsAvailable = false };

    public bool IsAvailable { get; init; } = true;

    /// <summary>Percentage of time the disks were busy (100 - % Idle Time), like Task Manager's "Active time".</summary>
    public double ActivePercent { get; init; }
    public double ReadBytesPerSecond { get; init; }
    public double WriteBytesPerSecond { get; init; }
}
