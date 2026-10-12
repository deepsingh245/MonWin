namespace SystemMonitor.Services;

/// <summary>
/// Raw disk counter access, split out from <see cref="DiskMonitorService"/> so the
/// projection logic can be unit-tested without live performance counters.
/// </summary>
public interface IDiskDataSource
{
    /// <summary>Reads aggregate (all physical disks) counters. Returns false when they are unavailable.</summary>
    bool TryRead(out float idlePercent, out float readBytesPerSecond, out float writeBytesPerSecond);
}
