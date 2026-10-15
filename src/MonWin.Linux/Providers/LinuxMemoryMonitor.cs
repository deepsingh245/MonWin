using SystemMonitor.Models;
using SystemMonitor.Services;

namespace MonWin.Linux.Providers;

/// <summary>RAM usage from /proc/meminfo (MemTotal and MemAvailable).</summary>
internal sealed class LinuxMemoryMonitor(LinuxFiles files, ILoggingService logger) : IMemoryMonitor
{
    public MemoryMetrics Sample()
    {
        try
        {
            return MemInfoParser.TryParse(files.ReadText("/proc/meminfo"), out var total, out var available)
                ? MemoryMetrics.FromTotals(total, available)
                : MemoryMetrics.Unavailable;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Memory sampling failed: {ex.Message}");
            return MemoryMetrics.Unavailable;
        }
    }
}
