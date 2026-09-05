using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface IGpuMonitor
{
    /// <summary>All physical adapters detected on this system (best-effort; empty if none/unavailable).</summary>
    IReadOnlyList<GpuAdapterInfo> Adapters { get; }

    /// <summary>Samples the currently selected (or auto-detected) adapter. Never throws.</summary>
    GpuMetrics Sample(int? selectedAdapterIndex);
}
