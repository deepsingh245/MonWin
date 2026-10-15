using SystemMonitor.Models;
using SystemMonitor.Services;

namespace MonWin.Linux.Providers;

/// <summary>
/// GPU monitoring from whatever the machine exposes without privileges: NVML for NVIDIA,
/// amdgpu sysfs for AMD, DRM identity for everything else. Every source is optional; a GPU
/// whose driver reports no utilization shows "N/A" (UsagePercent == null), never 0%.
/// </summary>
internal sealed class LinuxGpuMonitor : IGpuMonitor, IDisposable
{
    private readonly IReadOnlyList<IGpuAdapterSource> _sources;
    private readonly ILoggingService _logger;
    private readonly INvmlApi? _nvml;
    private int _disposed;

    public IReadOnlyList<GpuAdapterInfo> Adapters { get; }

    public LinuxGpuMonitor(IReadOnlyList<IGpuAdapterSource> sources, ILoggingService logger, INvmlApi? nvml = null)
    {
        _sources = sources;
        _logger = logger;
        _nvml = nvml;
        Adapters = sources
            .Select((s, i) => new GpuAdapterInfo(i, s.Id, s.Name, Safe(s).TotalBytes))
            .ToList();
    }

    /// <summary>Probes this machine: NVML devices first, then DRM cards (skipping NVIDIA cards NVML already covers).</summary>
    /// <param name="nvmlFactory">Loads NVML; tests inject a fake (or "none") instead of probing the real library.</param>
    public static LinuxGpuMonitor Discover(LinuxFiles files, ILoggingService logger, Func<INvmlApi?>? nvmlFactory = null)
    {
        var sources = new List<IGpuAdapterSource>();
        var nvml = nvmlFactory is null ? NvmlApi.TryCreate(logger) : nvmlFactory();
        if (nvml is not null)
        {
            for (var i = 0; i < nvml.DeviceCount; i++)
            {
                sources.Add(new NvmlGpuSource(nvml, i));
            }
        }

        foreach (var drm in DrmGpuSource.Discover(files))
        {
            // With NVML available it already reports every NVIDIA GPU (the proprietary driver
            // may not even create a DRM node), so listing the DRM card again would duplicate it.
            if (nvml is not null && drm.VendorId == DrmGpuSource.VendorNvidia)
            {
                continue;
            }

            sources.Add(drm);
        }

        return new LinuxGpuMonitor(sources, logger, nvml);
    }

    public GpuMetrics Sample(int? selectedAdapterIndex)
    {
        try
        {
            if (_sources.Count == 0)
            {
                return GpuMetrics.Unavailable;
            }

            if (selectedAdapterIndex is { } chosen && chosen >= 0 && chosen < _sources.Count)
            {
                return ToMetrics(_sources[chosen], _sources[chosen].Read());
            }

            // Auto-select like the Windows monitor: the busiest GPU; when no GPU reports
            // utilization at all, fall back to the first one so its name and memory still show.
            var readings = _sources.Select(s => (Source: s, Reading: Safe(s))).ToList();
            var busiest = readings
                .Where(r => r.Reading.UsagePercent.HasValue)
                .OrderByDescending(r => r.Reading.UsagePercent)
                .FirstOrDefault();
            var pick = busiest.Source is not null ? busiest : readings[0];
            return ToMetrics(pick.Source, pick.Reading);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPU sampling failed: {ex.Message}");
            return GpuMetrics.Unavailable;
        }
    }

    private static GpuMetrics ToMetrics(IGpuAdapterSource source, GpuReading reading) => new()
    {
        IsAvailable = true,
        Name = source.Name,
        UsagePercent = reading.UsagePercent,
        UsedMemoryBytes = reading.UsedBytes,
        TotalMemoryBytes = reading.TotalBytes,
    };

    private GpuReading Safe(IGpuAdapterSource source)
    {
        try
        {
            return source.Read();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPU '{source.Id}' read failed: {ex.Message}");
            return GpuReading.Unsupported;
        }
    }

    /// <summary>Idempotent: both SystemMonitorService and the DI container dispose the GPU monitor.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _nvml?.Dispose();
        }
    }
}
