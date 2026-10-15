namespace SystemMonitor.Models;

/// <summary>Immutable snapshot of physical RAM state for a single poll.</summary>
public sealed record MemoryMetrics
{
    public static readonly MemoryMetrics Unavailable = new() { IsAvailable = false };

    public bool IsAvailable { get; init; } = true;
    public double UsagePercent { get; init; }
    public ulong UsedBytes { get; init; }
    public ulong AvailableBytes { get; init; }
    public ulong TotalBytes { get; init; }

    public double UsedGigabytes => UsedBytes / 1024.0 / 1024.0 / 1024.0;
    public double TotalGigabytes => TotalBytes / 1024.0 / 1024.0 / 1024.0;
}
