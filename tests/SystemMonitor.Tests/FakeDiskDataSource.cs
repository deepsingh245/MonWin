using SystemMonitor.Services;

namespace SystemMonitor.Tests;

internal sealed class FakeDiskDataSource : IDiskDataSource
{
    public bool Available { get; set; } = true;
    public float IdlePercent { get; set; } = 100f;
    public float ReadBytesPerSecond { get; set; }
    public float WriteBytesPerSecond { get; set; }

    public bool TryRead(out float idlePercent, out float readBytesPerSecond, out float writeBytesPerSecond)
    {
        idlePercent = IdlePercent;
        readBytesPerSecond = ReadBytesPerSecond;
        writeBytesPerSecond = WriteBytesPerSecond;
        return Available;
    }
}
