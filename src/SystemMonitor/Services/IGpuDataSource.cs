using SystemMonitor.Native;

namespace SystemMonitor.Services;

/// <summary>
/// Abstraction over the raw OS data GpuMonitorService needs, so aggregation logic can
/// be unit tested with synthetic data instead of requiring real GPU hardware/counters.
/// </summary>
public interface IGpuDataSource
{
    bool EngineCategoryExists();
    bool MemoryCategoryExists();

    /// <summary>Utilization percentage per (luid, engineType) instance, sampled once.</summary>
    IReadOnlyList<GpuEngineSample> ReadEngineSamples();

    /// <summary>Dedicated GPU memory bytes currently in use, summed per luid across process instances.</summary>
    IReadOnlyDictionary<string, ulong> ReadMemoryUsageByLuid();

    IReadOnlyList<WmiAdapterRaw> GetAdapters();
}
