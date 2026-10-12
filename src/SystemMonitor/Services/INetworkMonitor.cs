using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface INetworkMonitor
{
    /// <summary>Samples current network throughput. Never throws — returns <see cref="NetworkMetrics.Unavailable"/> on failure.</summary>
    NetworkMetrics Sample();
}
