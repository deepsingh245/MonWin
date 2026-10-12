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

    /// <summary>Combined download + upload, in bytes/second.</summary>
    RollingHistory NetworkHistory { get; }

    /// <summary>Disk active time, in percent.</summary>
    RollingHistory DiskHistory { get; }

    void ApplySettings(AppSettings settings);
}
