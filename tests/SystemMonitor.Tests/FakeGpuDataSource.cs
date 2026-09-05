using SystemMonitor.Native;
using SystemMonitor.Services;

namespace SystemMonitor.Tests;

internal sealed class FakeGpuDataSource : IGpuDataSource
{
    public bool HasEngineCategory { get; set; } = true;
    public bool HasMemoryCategory { get; set; } = true;
    public List<GpuEngineSample> EngineSamples { get; set; } = [];
    public Dictionary<string, ulong> MemoryByLuid { get; set; } = [];
    public List<WmiAdapterRaw> Adapters { get; set; } = [];

    public bool EngineCategoryExists() => HasEngineCategory;
    public bool MemoryCategoryExists() => HasMemoryCategory;
    public IReadOnlyList<GpuEngineSample> ReadEngineSamples() => EngineSamples;
    public IReadOnlyDictionary<string, ulong> ReadMemoryUsageByLuid() => MemoryByLuid;
    public IReadOnlyList<WmiAdapterRaw> GetAdapters() => Adapters;
}
