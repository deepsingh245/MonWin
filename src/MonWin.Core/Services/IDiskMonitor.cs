using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface IDiskMonitor
{
    /// <summary>Samples current disk activity. Never throws — returns <see cref="DiskMetrics.Unavailable"/> on failure.</summary>
    DiskMetrics Sample();
}
