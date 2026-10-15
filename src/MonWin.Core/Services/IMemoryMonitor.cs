using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface IMemoryMonitor
{
    /// <summary>Samples current RAM state. Never throws — returns <see cref="MemoryMetrics.Unavailable"/> on failure.</summary>
    MemoryMetrics Sample();
}
