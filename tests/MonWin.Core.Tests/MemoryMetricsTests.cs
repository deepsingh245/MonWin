using SystemMonitor.Models;
using Xunit;

namespace SystemMonitor.Tests;

public class MemoryMetricsTests
{
    [Fact]
    public void FromTotals_ComputesUsedAndPercent()
    {
        var m = MemoryMetrics.FromTotals(totalBytes: 8_000, availableBytes: 2_000);

        Assert.True(m.IsAvailable);
        Assert.Equal(6_000UL, m.UsedBytes);
        Assert.Equal(75.0, m.UsagePercent, 3);
    }

    [Fact]
    public void FromTotals_AvailableAboveTotal_ClampsToZeroUsed()
    {
        var m = MemoryMetrics.FromTotals(1_000, 5_000);

        Assert.Equal(0UL, m.UsedBytes);
        Assert.Equal(0.0, m.UsagePercent);
    }

    [Fact]
    public void FromTotals_ZeroTotal_IsUnavailable()
    {
        Assert.False(MemoryMetrics.FromTotals(0, 0).IsAvailable);
    }
}
