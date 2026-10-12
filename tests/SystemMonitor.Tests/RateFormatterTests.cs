using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class RateFormatterTests
{
    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(-5, "0 B/s")]
    [InlineData(1023, "1023 B/s")]
    [InlineData(1024, "1.0 KB/s")]
    [InlineData(87_040, "85 KB/s")]
    [InlineData(1_258_291, "1.2 MB/s")]
    [InlineData(1_073_741_824, "1.0 GB/s")]
    public void Format_PicksUnitAndPrecision(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, RateFormatter.Format(bytesPerSecond));
    }
}
