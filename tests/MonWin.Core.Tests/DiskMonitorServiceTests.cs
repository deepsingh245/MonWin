using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class DiskMonitorServiceTests
{
    [Fact]
    public void Sample_DataSourceUnavailable_ReturnsUnavailable()
    {
        var service = new DiskMonitorService(new FakeDiskDataSource { Available = false }, new NoOpLoggingService());

        Assert.False(service.Sample().IsAvailable);
    }

    [Fact]
    public void Sample_ConvertsIdleToActiveAndPassesRates()
    {
        var source = new FakeDiskDataSource { IdlePercent = 75f, ReadBytesPerSecond = 1024f, WriteBytesPerSecond = 2048f };
        var service = new DiskMonitorService(source, new NoOpLoggingService());

        var result = service.Sample();

        Assert.True(result.IsAvailable);
        Assert.Equal(25, result.ActivePercent);
        Assert.Equal(1024, result.ReadBytesPerSecond);
        Assert.Equal(2048, result.WriteBytesPerSecond);
    }

    [Theory]
    [InlineData(110f, 0)]
    [InlineData(-5f, 100)]
    public void BuildFrom_ClampsActivePercent(float idle, double expectedActive)
    {
        Assert.Equal(expectedActive, DiskMonitorService.BuildFrom(idle, 0, 0).ActivePercent);
    }

    [Fact]
    public void BuildFrom_NegativeRates_ClampToZero()
    {
        var result = DiskMonitorService.BuildFrom(50f, -1f, -1f);

        Assert.Equal(0, result.ReadBytesPerSecond);
        Assert.Equal(0, result.WriteBytesPerSecond);
    }
}
