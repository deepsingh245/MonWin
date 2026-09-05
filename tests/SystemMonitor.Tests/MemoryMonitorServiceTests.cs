using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class MemoryMonitorServiceTests
{
    [Fact]
    public void BuildFrom_TypicalValues_ComputesUsedAndPercent()
    {
        const ulong total = 32UL * 1024 * 1024 * 1024;
        const ulong avail = 16UL * 1024 * 1024 * 1024;

        var metrics = MemoryMonitorService.BuildFrom(memoryLoadPercent: 50, totalPhys: total, availPhys: avail);

        Assert.True(metrics.IsAvailable);
        Assert.Equal(50.0, metrics.UsagePercent);
        Assert.Equal(total - avail, metrics.UsedBytes);
        Assert.Equal(avail, metrics.AvailableBytes);
        Assert.Equal(total, metrics.TotalBytes);
    }

    [Fact]
    public void BuildFrom_AvailableExceedsTotal_ClampsUsedToZero()
    {
        var metrics = MemoryMonitorService.BuildFrom(memoryLoadPercent: 0, totalPhys: 100, availPhys: 200);
        Assert.Equal(0UL, metrics.UsedBytes);
    }

    [Theory]
    [InlineData(150u, 100)]
    [InlineData(0u, 0)]
    public void BuildFrom_OutOfRangePercent_Clamps(uint input, double expected)
    {
        var metrics = MemoryMonitorService.BuildFrom(memoryLoadPercent: input, totalPhys: 100, availPhys: 50);
        Assert.Equal(expected, metrics.UsagePercent);
    }
}
