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

    [Theory]
    [InlineData(0, "0 bps")]
    [InlineData(100, "800 bps")]
    [InlineData(125, "1.0 Kbps")]          // 1000 bits — bits use a 1000 base
    [InlineData(1_250_000, "10 Mbps")]
    [InlineData(125_000_000, "1.0 Gbps")]
    public void Format_Bits_UsesDecimalBase(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, RateFormatter.Format(bytesPerSecond, bits: true));
    }

    [Fact]
    public void Format_IgnoresCurrentCulture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.2 MB/s", RateFormatter.Format(1_258_291));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }
}
