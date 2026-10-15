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

    /// <summary>
    /// Builds a reading from total/available bytes for OSes that don't report a load
    /// percentage (Linux /proc/meminfo). Percentage is computed from used/total.
    /// </summary>
    public static MemoryMetrics FromTotals(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0)
        {
            return Unavailable;
        }

        var available = Math.Min(availableBytes, totalBytes);
        var used = totalBytes - available;
        return new MemoryMetrics
        {
            IsAvailable = true,
            UsagePercent = Math.Clamp(used * 100.0 / totalBytes, 0, 100),
            UsedBytes = used,
            AvailableBytes = available,
            TotalBytes = totalBytes,
        };
    }

    public double UsedGigabytes => UsedBytes / 1024.0 / 1024.0 / 1024.0;
    public double TotalGigabytes => TotalBytes / 1024.0 / 1024.0 / 1024.0;
}
