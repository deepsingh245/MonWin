using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface ICpuMonitor
{
    /// <summary>Samples current CPU state. Never throws — returns <see cref="CpuMetrics.Unavailable"/> on failure.</summary>
    CpuMetrics Sample();
}
