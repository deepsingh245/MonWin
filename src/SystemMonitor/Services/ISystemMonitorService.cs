using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface ISystemMonitorService : IDisposable
{
    event EventHandler<MonitorSnapshot>? SnapshotUpdated;

    bool IsPaused { get; }
    void Pause();
    void Resume();

    RollingHistory CpuHistory { get; }
    RollingHistory MemoryHistory { get; }
    RollingHistory GpuHistory { get; }

    void ApplySettings(AppSettings settings);
}
