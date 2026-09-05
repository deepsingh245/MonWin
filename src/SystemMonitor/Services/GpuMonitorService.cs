using SystemMonitor.Models;

namespace SystemMonitor.Services;

public sealed class GpuMonitorService : IGpuMonitor, IDisposable
{
    private readonly IGpuDataSource _dataSource;
    private readonly ILoggingService _logger;
    private List<string> _luidOrder = [];
    private IReadOnlyList<GpuAdapterInfo> _adapters = [];
    private DateTime _lastAdapterRefresh = DateTime.MinValue;
    private static readonly TimeSpan AdapterRefreshInterval = TimeSpan.FromSeconds(10);

    public GpuMonitorService(IGpuDataSource dataSource, ILoggingService logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public IReadOnlyList<GpuAdapterInfo> Adapters => _adapters;

    public GpuMetrics Sample(int? selectedAdapterIndex)
    {
        try
        {
            if (!_dataSource.EngineCategoryExists())
            {
                _logger.LogWarning("GPU Engine performance counter category not found; GPU utilization unavailable.");
                RefreshAdaptersIfDue();
                return _adapters.Count > 0
                    ? GpuMetrics.Unavailable with { Name = _adapters[0].Name }
                    : GpuMetrics.Unavailable;
            }

            var engineSamples = _dataSource.ReadEngineSamples();
            var memoryByLuid = _dataSource.MemoryCategoryExists()
                ? _dataSource.ReadMemoryUsageByLuid()
                : new Dictionary<string, ulong>();

            RefreshLuidOrder(engineSamples);
            RefreshAdaptersIfDue();

            return Aggregate(engineSamples, memoryByLuid, selectedAdapterIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPU sampling failed: {ex.Message}");
            return GpuMetrics.Unavailable;
        }
    }

    private void RefreshLuidOrder(IReadOnlyList<Native.GpuEngineSample> samples)
    {
        foreach (var s in samples)
        {
            if (!_luidOrder.Contains(s.Luid))
            {
                _luidOrder.Add(s.Luid);
            }
        }
    }

    private void RefreshAdaptersIfDue()
    {
        var now = DateTime.UtcNow;
        if (now - _lastAdapterRefresh < AdapterRefreshInterval && _adapters.Count > 0)
        {
            return;
        }

        _lastAdapterRefresh = now;
        var wmiAdapters = _dataSource.GetAdapters();
        var list = new List<GpuAdapterInfo>();
        for (var i = 0; i < wmiAdapters.Count; i++)
        {
            var luid = i < _luidOrder.Count ? _luidOrder[i] : $"unmapped_{i}";
            list.Add(new GpuAdapterInfo(i, luid, wmiAdapters[i].Name, wmiAdapters[i].AdapterRamBytes));
        }

        _adapters = list;
    }

    /// <summary>
    /// Aggregates raw engine/memory samples into a single GpuMetrics for the selected
    /// (or auto-detected) adapter. Per-adapter utilization uses the max across engine
    /// types (engines run concurrently, so summing overstates load), not a naive sum.
    /// Extracted as an internal static so it's unit-testable with synthetic data.
    /// </summary>
    internal static GpuMetrics Aggregate(
        IReadOnlyList<Native.GpuEngineSample> engineSamples,
        IReadOnlyDictionary<string, ulong> memoryByLuid,
        int? selectedAdapterIndex,
        IReadOnlyList<GpuAdapterInfo> adapters,
        IReadOnlyList<string> luidOrder)
    {
        if (engineSamples.Count == 0)
        {
            return adapters.Count > 0 ? GpuMetrics.Unavailable with { Name = adapters[0].Name } : GpuMetrics.Unavailable;
        }

        var maxByLuid = new Dictionary<string, double>();
        foreach (var sample in engineSamples)
        {
            var current = maxByLuid.GetValueOrDefault(sample.Luid);
            maxByLuid[sample.Luid] = Math.Max(current, Math.Clamp(sample.UtilizationPercent, 0, 100));
        }

        string targetLuid;
        string adapterName;
        ulong? totalMemory = null;

        if (selectedAdapterIndex is { } idx && idx >= 0 && idx < luidOrder.Count)
        {
            targetLuid = luidOrder[idx];
            adapterName = idx < adapters.Count ? adapters[idx].Name : "GPU";
            totalMemory = idx < adapters.Count ? adapters[idx].TotalMemoryBytes : null;
        }
        else
        {
            // Auto-select: the adapter currently doing the most work.
            targetLuid = maxByLuid.OrderByDescending(kv => kv.Value).First().Key;
            var matchIndex = IndexOf(luidOrder, targetLuid);
            adapterName = matchIndex >= 0 && matchIndex < adapters.Count ? adapters[matchIndex].Name : "GPU";
            totalMemory = matchIndex >= 0 && matchIndex < adapters.Count ? adapters[matchIndex].TotalMemoryBytes : null;
        }

        var utilization = maxByLuid.GetValueOrDefault(targetLuid);
        var usedMemory = memoryByLuid.GetValueOrDefault(targetLuid);

        return new GpuMetrics
        {
            IsAvailable = true,
            Name = adapterName,
            UsagePercent = utilization,
            UsedMemoryBytes = usedMemory > 0 ? usedMemory : null,
            TotalMemoryBytes = totalMemory,
        };
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private GpuMetrics Aggregate(
        IReadOnlyList<Native.GpuEngineSample> engineSamples,
        IReadOnlyDictionary<string, ulong> memoryByLuid,
        int? selectedAdapterIndex) =>
        Aggregate(engineSamples, memoryByLuid, selectedAdapterIndex, _adapters, _luidOrder);

    public void Dispose() => (_dataSource as IDisposable)?.Dispose();
}
