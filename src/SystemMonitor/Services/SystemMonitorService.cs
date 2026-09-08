using SystemMonitor.Models;

namespace SystemMonitor.Services;

/// <summary>
/// Orchestrates polling of CPU/RAM/GPU on a single background timer, maintains rolling
/// history buffers sized from settings, and raises <see cref="SnapshotUpdated"/> on the
/// thread the timer fires on — callers (ViewModels) marshal to the UI dispatcher
/// themselves, keeping this class UI-framework-agnostic and testable.
/// </summary>
public sealed class SystemMonitorService : ISystemMonitorService
{
    private readonly ICpuMonitor _cpuMonitor;
    private readonly IMemoryMonitor _memoryMonitor;
    private readonly IGpuMonitor _gpuMonitor;
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _logger;
    private readonly object _sync = new();

    private Timer? _timer;
    private int _updateIntervalMs;
    private volatile bool _isPaused;

    public event EventHandler<MonitorSnapshot>? SnapshotUpdated;

    public bool IsPaused => _isPaused;

    public RollingHistory CpuHistory { get; private set; }
    public RollingHistory MemoryHistory { get; private set; }
    public RollingHistory GpuHistory { get; private set; }

    public SystemMonitorService(
        ICpuMonitor cpuMonitor,
        IMemoryMonitor memoryMonitor,
        IGpuMonitor gpuMonitor,
        ISettingsService settingsService,
        ILoggingService logger)
    {
        _cpuMonitor = cpuMonitor;
        _memoryMonitor = memoryMonitor;
        _gpuMonitor = gpuMonitor;
        _settingsService = settingsService;
        _logger = logger;

        var settings = _settingsService.Current;
        _updateIntervalMs = settings.UpdateIntervalMs;
        var capacity = HistoryCapacity(settings);
        CpuHistory = new RollingHistory(capacity);
        MemoryHistory = new RollingHistory(capacity);
        GpuHistory = new RollingHistory(capacity);

        _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_updateIntervalMs));
    }

    public void Pause() => _isPaused = true;

    public void Resume() => _isPaused = false;

    public void ApplySettings(AppSettings settings)
    {
        lock (_sync)
        {
            var capacity = HistoryCapacity(settings);
            if (capacity != CpuHistory.Capacity)
            {
                CpuHistory.Resize(capacity);
                MemoryHistory.Resize(capacity);
                GpuHistory.Resize(capacity);
            }

            if (settings.UpdateIntervalMs != _updateIntervalMs)
            {
                _updateIntervalMs = settings.UpdateIntervalMs;
                _timer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(_updateIntervalMs));
            }
        }
    }

    private static int HistoryCapacity(AppSettings settings) =>
        Math.Max(1, settings.HistorySeconds * 1000 / Math.Max(1, settings.UpdateIntervalMs));

    private int _tickInProgress;

    private void OnTick(object? state)
    {
        if (_isPaused)
        {
            return;
        }

        // System.Threading.Timer callbacks are not serialized against each other — if a
        // sample takes longer than one interval (slow perf-counter refresh, GC pause,
        // etc.) the next tick can fire while the previous one is still running. The
        // GPU data source's internal caches aren't thread-safe (no caller previously
        // needed them to be), so overlapping ticks were corrupting them. Skip a tick
        // outright rather than overlap — a single missed 250ms-2s sample is invisible
        // to the user, a corrupted dictionary is not.
        if (Interlocked.CompareExchange(ref _tickInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var cpu = _cpuMonitor.Sample();
            var memory = _memoryMonitor.Sample();
            var gpu = _gpuMonitor.Sample(_settingsService.Current.SelectedGpuAdapterIndex);

            lock (_sync)
            {
                CpuHistory.Add(cpu.IsAvailable ? cpu.UsagePercent : 0);
                MemoryHistory.Add(memory.IsAvailable ? memory.UsagePercent : 0);
                GpuHistory.Add(gpu.IsAvailable ? gpu.UsagePercent ?? 0 : 0);
            }

            var snapshot = new MonitorSnapshot(DateTimeOffset.Now, cpu, memory, gpu);
            SnapshotUpdated?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError("Monitor poll failed", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _tickInProgress, 0);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        (_gpuMonitor as IDisposable)?.Dispose();
    }
}
