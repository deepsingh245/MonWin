namespace SystemMonitor.Models;

/// <summary>Immutable snapshot of CPU state for a single poll.</summary>
public sealed record CpuMetrics
{
    public static readonly CpuMetrics Unavailable = new()
    {
        IsAvailable = false,
        ProcessorName = "Unknown",
        LogicalCoreCount = Environment.ProcessorCount,
    };

    public bool IsAvailable { get; init; } = true;
    public double UsagePercent { get; init; }
    public double? FrequencyMhz { get; init; }
    public string ProcessorName { get; init; } = string.Empty;
    public int LogicalCoreCount { get; init; }
}
