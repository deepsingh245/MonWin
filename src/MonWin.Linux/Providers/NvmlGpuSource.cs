namespace MonWin.Linux.Providers;

/// <summary>One NVIDIA GPU read through NVML.</summary>
internal sealed class NvmlGpuSource(INvmlApi nvml, int index) : IGpuAdapterSource
{
    public string Id => $"nvml:{index}";

    public string Name => nvml.GetName(index) ?? $"NVIDIA GPU {index}";

    public GpuReading Read()
    {
        double? usage = nvml.TryGetUtilization(index, out var percent) ? percent : null;
        return nvml.TryGetMemory(index, out var total, out var used)
            ? new GpuReading(usage, used, total)
            : new GpuReading(usage, null, null);
    }
}
