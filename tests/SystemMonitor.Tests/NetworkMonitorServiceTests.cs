using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class NetworkMonitorServiceTests
{
    [Fact]
    public void RatePerSecond_DividesDeltaByElapsed()
    {
        Assert.Equal(2048, NetworkMonitorService.RatePerSecond(1000, 2024, 0.5));
    }

    [Fact]
    public void RatePerSecond_CounterDropped_ReturnsZero()
    {
        // An adapter disconnecting removes its bytes from the summed total.
        Assert.Equal(0, NetworkMonitorService.RatePerSecond(5000, 1000, 1.0));
    }

    [Fact]
    public void RatePerSecond_ZeroElapsed_ReturnsZero()
    {
        Assert.Equal(0, NetworkMonitorService.RatePerSecond(0, 1000, 0));
    }

    [Fact]
    public void Sample_FirstCall_ReportsZeroRates()
    {
        var service = new NetworkMonitorService(new NoOpLoggingService());

        var result = service.Sample();

        Assert.True(result.IsAvailable);
        Assert.Equal(0, result.DownloadBytesPerSecond);
        Assert.Equal(0, result.UploadBytesPerSecond);
    }
}
