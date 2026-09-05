namespace SystemMonitor.Models;

/// <summary>A single point-in-time reading across all metrics, produced once per poll interval.</summary>
public sealed record MonitorSnapshot(
    DateTimeOffset Timestamp,
    CpuMetrics Cpu,
    MemoryMetrics Memory,
    GpuMetrics Gpu);
