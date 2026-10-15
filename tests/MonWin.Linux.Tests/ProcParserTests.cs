using MonWin.Linux.Providers;
using Xunit;

namespace MonWin.Linux.Tests;

public class ProcParserTests
{
    private const string ProcStat =
        "cpu  100 20 50 800 30 5 10 2 0 0\ncpu0 50 10 25 400 15 2 5 1 0 0\ncpu1 50 10 25 400 15 3 5 1 0 0\nintr 12345\n";

    [Fact]
    public void ProcStat_MapsJiffiesToWindowsStyleTicks()
    {
        Assert.True(ProcStatParser.TryParse(ProcStat, out var ticks));

        Assert.Equal(830UL, ticks.Idle);              // idle + iowait
        Assert.Equal(50UL + 5 + 10 + 830, ticks.Kernel); // system + irq + softirq + idle + iowait
        Assert.Equal(100UL + 20 + 2, ticks.User);     // user + nice + steal
    }

    [Fact]
    public void ProcStat_UsesAggregateLineNotPerCore()
    {
        Assert.True(ProcStatParser.TryParse("cpu0 1 1 1 1 1 1 1 1\ncpu  10 0 10 80 0 0 0 0\n", out var ticks));

        Assert.Equal(80UL, ticks.Idle);
    }

    [Fact]
    public void ProcStat_OldKernelWithFewColumns_StillParses()
    {
        Assert.True(ProcStatParser.TryParse("cpu  100 0 50 850\n", out var ticks));

        Assert.Equal(850UL, ticks.Idle);
        Assert.Equal(900UL, ticks.Kernel);
        Assert.Equal(100UL, ticks.User);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("cpu  1 2\n")]
    [InlineData("cpu  a b c d e f g h\n")]
    [InlineData("cpu  -1 0 0 0 0 0 0 0\n")]
    public void ProcStat_InvalidInput_IsRejected(string? text)
    {
        Assert.False(ProcStatParser.TryParse(text, out _));
    }

    [Fact]
    public void CpuInfo_ReadsModelNameAndCollapsesSpaces()
    {
        const string text = "processor\t: 0\nmodel name\t: AMD  Ryzen 7   5800X 8-Core Processor\ncpu MHz\t\t: 3800.000\n";

        Assert.Equal("AMD Ryzen 7 5800X 8-Core Processor", CpuInfoParser.ParseModelName(text));
    }

    [Fact]
    public void CpuInfo_ArmPrefersBoardModelOverSocName()
    {
        const string text = "Hardware\t: BCM2711\nModel\t\t: Raspberry Pi 4 Model B\n";

        Assert.Equal("Raspberry Pi 4 Model B", CpuInfoParser.ParseModelName(text));
        Assert.Equal("BCM2711", CpuInfoParser.ParseModelName("Hardware\t: BCM2711\n"));
        Assert.Null(CpuInfoParser.ParseModelName(""));
    }

    [Fact]
    public void CpuInfo_AveragesMhzAcrossCores()
    {
        const string text = "cpu MHz\t\t: 2000.000\ncpu MHz\t\t: 3000.500\n";

        Assert.Equal(2500.25, CpuInfoParser.ParseAverageMhz(text));
    }

    [Fact]
    public void CpuInfo_NoMhz_ReturnsNull()
    {
        Assert.Null(CpuInfoParser.ParseAverageMhz("model name : x\n"));
    }

    [Fact]
    public void MemInfo_ReadsTotalAndAvailableInBytes()
    {
        const string text = "MemTotal:       16000000 kB\nMemFree:         1000000 kB\nMemAvailable:    8000000 kB\nBuffers: 1 kB\n";

        Assert.True(MemInfoParser.TryParse(text, out var total, out var available));

        Assert.Equal(16_000_000UL * 1024, total);
        Assert.Equal(8_000_000UL * 1024, available);
    }

    [Fact]
    public void MemInfo_WithoutMemAvailable_EstimatesFromFreeBuffersCache()
    {
        const string text = "MemTotal: 1000 kB\nMemFree: 100 kB\nBuffers: 50 kB\nCached: 200 kB\nSReclaimable: 25 kB\n";

        Assert.True(MemInfoParser.TryParse(text, out _, out var available));

        Assert.Equal((100UL + 50 + 200 + 25) * 1024, available);
    }

    [Fact]
    public void MemInfo_AvailableAboveTotal_IsClamped()
    {
        Assert.True(MemInfoParser.TryParse("MemTotal: 100 kB\nMemAvailable: 500 kB\n", out var total, out var available));

        Assert.Equal(total, available);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MemAvailable: 100 kB\n")]  // no MemTotal
    [InlineData("MemTotal: 0 kB\n")]
    [InlineData("MemTotal: abc kB\n")]
    public void MemInfo_InvalidInput_IsRejected(string? text)
    {
        Assert.False(MemInfoParser.TryParse(text, out _, out _));
    }

    [Fact]
    public void DiskStats_ParsesCountersAndSkipsShortLines()
    {
        const string text =
            " 259       0 nvme0n1 100 0 2000 50 200 0 4000 80 0 1000 130\n" +
            "   8       0 sda 1 2 3\n" +
            "\n";

        var parsed = DiskStatsParser.Parse(text);

        var disk = Assert.Single(parsed);
        Assert.Equal(new DiskCounters("nvme0n1", 2000, 4000, 1000), disk);
    }

    [Theory]
    [InlineData("nvme0n1", true)]
    [InlineData("sda", true)]
    [InlineData("nvme0n1p1", false)] // partition: not listed in /sys/block
    [InlineData("loop0", false)]
    [InlineData("dm-0", false)]
    [InlineData("md127", false)]
    [InlineData("zram0", false)]
    [InlineData("sr0", false)]
    public void DiskStats_WholePhysicalDeviceFilter(string name, bool expected)
    {
        var wholeDevicesInSysBlock = new HashSet<string> { "nvme0n1", "sda", "loop0", "dm-0", "md127", "zram0", "sr0" };

        Assert.Equal(expected, DiskStatsParser.IsWholePhysicalDevice(name, wholeDevicesInSysBlock.Contains));
    }
}
