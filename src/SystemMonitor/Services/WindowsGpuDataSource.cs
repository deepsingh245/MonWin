using System.Diagnostics;
using SystemMonitor.Native;

namespace SystemMonitor.Services;

/// <summary>
/// Live implementation of <see cref="IGpuDataSource"/> backed by Windows performance
/// counters and WMI (see <see cref="GpuInterop"/>). Performance counters that measure a
/// rate (like "Utilization Percentage") need two samples to be meaningful, so instances
/// are cached across polls rather than recreated each time.
/// </summary>
public sealed class WindowsGpuDataSource : IGpuDataSource, IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _engineCounters = new();
    private readonly Dictionary<string, PerformanceCounter> _dedicatedMemoryCounters = new();
    private readonly Dictionary<string, PerformanceCounter> _sharedMemoryCounters = new();
    private readonly ILoggingService _logger;

    public WindowsGpuDataSource(ILoggingService logger)
    {
        _logger = logger;
    }

    public bool EngineCategoryExists() => SafeCall(GpuInterop.EngineCategoryExists, false);

    public bool MemoryCategoryExists() => SafeCall(GpuInterop.MemoryCategoryExists, false);

    public IReadOnlyList<GpuEngineSample> ReadEngineSamples()
    {
        var samples = new List<GpuEngineSample>();
        try
        {
            var instanceNames = GpuInterop.GetEngineInstanceNames();
            var liveInstances = new HashSet<string>(instanceNames);
            PruneStale(_engineCounters, liveInstances);

            // Iterate the original (order-preserving) array, not the HashSet, so callers
            // that rely on first-seen order for adapter correlation stay stable.
            foreach (var instanceName in instanceNames)
            {
                if (!GpuInterop.TryParseEngineInstance(instanceName, out var luid, out var engineType))
                {
                    continue;
                }

                if (!_engineCounters.TryGetValue(instanceName, out var counter))
                {
                    counter = GpuInterop.CreateEngineCounter(instanceName);
                    _engineCounters[instanceName] = counter;
                }

                var value = SafeNextValue(counter);
                samples.Add(new GpuEngineSample(luid, engineType, value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPU engine counter read failed: {ex.Message}");
        }

        return samples;
    }

    public IReadOnlyDictionary<string, ulong> ReadMemoryUsageByLuid()
    {
        var byLuid = new Dictionary<string, ulong>();
        try
        {
            var liveInstances = new HashSet<string>(GpuInterop.GetMemoryInstanceNames());
            PruneStale(_dedicatedMemoryCounters, liveInstances);
            PruneStale(_sharedMemoryCounters, liveInstances);

            foreach (var instanceName in liveInstances)
            {
                if (!GpuInterop.TryParseMemoryInstance(instanceName, out var luid))
                {
                    continue;
                }

                if (!_dedicatedMemoryCounters.TryGetValue(instanceName, out var counter))
                {
                    counter = GpuInterop.CreateMemoryDedicatedCounter(instanceName);
                    _dedicatedMemoryCounters[instanceName] = counter;
                }

                var bytes = (ulong)Math.Max(0, SafeNextValue(counter));
                byLuid[luid] = byLuid.GetValueOrDefault(luid) + bytes;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPU memory counter read failed: {ex.Message}");
        }

        return byLuid;
    }

    public IReadOnlyList<WmiAdapterRaw> GetAdapters() => SafeCall(GpuInterop.GetWmiAdapters, new List<WmiAdapterRaw>());

    private static void PruneStale(Dictionary<string, PerformanceCounter> cache, HashSet<string> live)
    {
        foreach (var stale in cache.Keys.Where(k => !live.Contains(k)).ToList())
        {
            cache[stale].Dispose();
            cache.Remove(stale);
        }
    }

    private float SafeNextValue(PerformanceCounter counter)
    {
        try
        {
            return counter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPU counter NextValue failed: {ex.Message}");
            return 0f;
        }
    }

    private T SafeCall<T>(Func<T> call, T fallback)
    {
        try { return call(); }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPU data source call failed: {ex.Message}");
            return fallback;
        }
    }

    public void Dispose()
    {
        foreach (var counter in _engineCounters.Values) counter.Dispose();
        foreach (var counter in _dedicatedMemoryCounters.Values) counter.Dispose();
        foreach (var counter in _sharedMemoryCounters.Values) counter.Dispose();
        _engineCounters.Clear();
        _dedicatedMemoryCounters.Clear();
        _sharedMemoryCounters.Clear();
    }
}
