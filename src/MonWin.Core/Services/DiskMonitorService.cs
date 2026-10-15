using SystemMonitor.Models;

namespace SystemMonitor.Services;

public sealed class DiskMonitorService : IDiskMonitor, IDisposable
{
    private readonly IDiskDataSource _dataSource;
    private readonly ILoggingService _logger;

    public DiskMonitorService(IDiskDataSource dataSource, ILoggingService logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public DiskMetrics Sample()
    {
        try
        {
            return _dataSource.TryRead(out var idle, out var read, out var write)
                ? BuildFrom(idle, read, write)
                : DiskMetrics.Unavailable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Disk sampling failed: {ex.Message}");
            return DiskMetrics.Unavailable;
        }
    }

    /// <summary>
    /// Pure projection from raw counter values, extracted for unit testing. "% Idle Time"
    /// can briefly exceed 100 or dip below 0 between counter refreshes, hence the clamps.
    /// </summary>
    internal static DiskMetrics BuildFrom(float idlePercent, float readBytesPerSecond, float writeBytesPerSecond) => new()
    {
        IsAvailable = true,
        ActivePercent = Math.Clamp(100.0 - idlePercent, 0, 100),
        ReadBytesPerSecond = Math.Max(0, readBytesPerSecond),
        WriteBytesPerSecond = Math.Max(0, writeBytesPerSecond),
    };

    public void Dispose() => (_dataSource as IDisposable)?.Dispose();
}
