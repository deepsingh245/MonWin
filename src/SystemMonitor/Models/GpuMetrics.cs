namespace SystemMonitor.Models;

/// <summary>Immutable snapshot of a single GPU adapter's state for a single poll.</summary>
public sealed record GpuMetrics
{
    public static readonly GpuMetrics Unavailable = new() { IsAvailable = false, Name = "N/A" };

    public bool IsAvailable { get; init; } = true;
    public string Name { get; init; } = string.Empty;
    public double? UsagePercent { get; init; }
    public ulong? UsedMemoryBytes { get; init; }

    /// <summary>Null when the reported total looks unreliable (e.g. AdapterRAM capped at 4GB by driver/WMI).</summary>
    public ulong? TotalMemoryBytes { get; init; }

    public double? UsedMemoryGigabytes => UsedMemoryBytes.HasValue ? UsedMemoryBytes.Value / 1024.0 / 1024.0 / 1024.0 : null;
    public double? TotalMemoryGigabytes => TotalMemoryBytes.HasValue ? TotalMemoryBytes.Value / 1024.0 / 1024.0 / 1024.0 : null;
}

/// <summary>Static identity of a GPU adapter, used for enumeration/selection in Settings.</summary>
public sealed record GpuAdapterInfo(int Index, string Luid, string Name, ulong? TotalMemoryBytes);
