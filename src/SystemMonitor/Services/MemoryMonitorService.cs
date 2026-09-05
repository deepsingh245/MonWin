using SystemMonitor.Models;
using SystemMonitor.Native;

namespace SystemMonitor.Services;

public sealed class MemoryMonitorService : IMemoryMonitor
{
    private readonly ILoggingService _logger;

    public MemoryMonitorService(ILoggingService logger)
    {
        _logger = logger;
    }

    public MemoryMetrics Sample()
    {
        try
        {
            var status = MemoryInterop.TryGetMemoryStatus();
            if (status is not { } s)
            {
                return MemoryMetrics.Unavailable;
            }

            return BuildFrom(s.DwMemoryLoad, s.UllTotalPhys, s.UllAvailPhys);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Memory sampling failed: {ex.Message}");
            return MemoryMetrics.Unavailable;
        }
    }

    /// <summary>Pure projection from raw GlobalMemoryStatusEx fields, extracted for unit testing.</summary>
    internal static MemoryMetrics BuildFrom(uint memoryLoadPercent, ulong totalPhys, ulong availPhys)
    {
        var used = totalPhys > availPhys ? totalPhys - availPhys : 0UL;
        return new MemoryMetrics
        {
            IsAvailable = true,
            UsagePercent = Math.Clamp(memoryLoadPercent, 0, 100),
            UsedBytes = used,
            AvailableBytes = availPhys,
            TotalBytes = totalPhys,
        };
    }
}
