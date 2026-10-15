namespace MonWin.Linux.Providers;

/// <summary>
/// One GPU reading. Each field is null when the hardware/driver doesn't expose it, which is
/// different from a measured 0: "N/A" and "0%" must never be conflated.
/// </summary>
internal readonly record struct GpuReading(double? UsagePercent, ulong? UsedBytes, ulong? TotalBytes)
{
    public static readonly GpuReading Unsupported = new(null, null, null);
}

/// <summary>A single physical GPU that can be polled. Implementations never throw.</summary>
internal interface IGpuAdapterSource
{
    /// <summary>Stable identifier (e.g. "card0" or "nvml:0"), shown in settings.</summary>
    string Id { get; }

    string Name { get; }

    GpuReading Read();
}
