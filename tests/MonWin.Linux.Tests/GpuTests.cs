using MonWin.Linux.Providers;
using SystemMonitor.Tests;
using Xunit;

namespace MonWin.Linux.Tests;

public class GpuTests
{
    private static readonly NoOpLoggingService Logger = new();

    private static void AddCard(FixtureRoot root, string card, string vendor, string device, string? busy = null, ulong? vramUsed = null, ulong? vramTotal = null, string? product = null)
    {
        root.Write($"/sys/class/drm/{card}/device/vendor", vendor + "\n");
        root.Write($"/sys/class/drm/{card}/device/device", device + "\n");
        if (busy is not null) root.Write($"/sys/class/drm/{card}/device/gpu_busy_percent", busy + "\n");
        if (vramUsed is not null) root.Write($"/sys/class/drm/{card}/device/mem_info_vram_used", vramUsed + "\n");
        if (vramTotal is not null) root.Write($"/sys/class/drm/{card}/device/mem_info_vram_total", vramTotal + "\n");
        if (product is not null) root.Write($"/sys/class/drm/{card}/device/product_name", product + "\n");
    }

    [Fact]
    public void Amd_ReportsUtilizationAndVram()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x1002", "0x73bf", busy: "37", vramUsed: 1_073_741_824, vramTotal: 8_589_934_592);
        var monitor = new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger);

        var gpu = monitor.Sample(null);

        Assert.True(gpu.IsAvailable);
        Assert.Equal(37.0, gpu.UsagePercent);
        Assert.Equal(1_073_741_824UL, gpu.UsedMemoryBytes);
        Assert.Equal(8_589_934_592UL, gpu.TotalMemoryBytes);
        Assert.Equal("AMD GPU [1002:73bf]", gpu.Name);
        Assert.Equal(8_589_934_592UL, monitor.Adapters[0].TotalMemoryBytes);
    }

    [Fact]
    public void Amd_UsesProductNameWhenTheDriverProvidesOne()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x1002", "0x73bf", busy: "0", product: "AMD Radeon RX 6800");

        Assert.Equal("AMD Radeon RX 6800", new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger).Sample(null).Name);
    }

    [Fact]
    public void Amd_ZeroUtilizationIsReportedAsZeroNotUnsupported()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x1002", "0x73bf", busy: "0");

        var gpu = new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger).Sample(null);

        Assert.Equal(0.0, gpu.UsagePercent);
    }

    [Fact]
    public void Intel_HasNoUtilization_ShowsUnsupportedNotZero()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x8086", "0x9a49");

        var gpu = new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger).Sample(null);

        Assert.True(gpu.IsAvailable);
        Assert.Null(gpu.UsagePercent);
        Assert.Equal("Intel GPU [8086:9a49]", gpu.Name);
    }

    [Fact]
    public void NoGpu_IsUnavailable()
    {
        using var root = new FixtureRoot();

        var monitor = new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger);

        Assert.False(monitor.Sample(null).IsAvailable);
        Assert.Empty(monitor.Adapters);
    }

    [Fact]
    public void ConnectorEntriesAndUnreadableCardsAreIgnored()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x1002", "0x73bf", busy: "5");
        root.MakeDirectory("/sys/class/drm/card0-HDMI-A-1");
        root.MakeDirectory("/sys/class/drm/renderD128");
        root.MakeDirectory("/sys/class/drm/card1"); // no device/vendor file

        var sources = DrmGpuSource.Discover(root.Files);

        Assert.Equal("card0", Assert.Single(sources).Id);
    }

    [Fact]
    public void BrokenGpuFiles_DoNotThrow()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x1002", "0x73bf", busy: "not a number", vramUsed: null);

        var gpu = new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger).Sample(null);

        Assert.True(gpu.IsAvailable);
        Assert.Null(gpu.UsagePercent);
    }

    [Fact]
    public void AutoSelect_PrefersTheGpuThatReportsUtilization()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x8086", "0x9a49");                // integrated Intel: no utilization
        AddCard(root, "card1", "0x1002", "0x73bf", busy: "64");    // discrete AMD
        var monitor = new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger);

        Assert.Equal(64.0, monitor.Sample(null).UsagePercent);
        Assert.Equal(2, monitor.Adapters.Count);
    }

    [Fact]
    public void ExplicitSelection_OverridesAutoSelect_AndBadIndexFallsBack()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x8086", "0x9a49");
        AddCard(root, "card1", "0x1002", "0x73bf", busy: "64");
        var monitor = new LinuxGpuMonitor(DrmGpuSource.Discover(root.Files), Logger);

        Assert.Null(monitor.Sample(0).UsagePercent);
        Assert.Equal(64.0, monitor.Sample(1).UsagePercent);
        Assert.Equal(64.0, monitor.Sample(99).UsagePercent);   // out of range → auto
        Assert.Equal(64.0, monitor.Sample(-1).UsagePercent);
    }

    [Fact]
    public void SourceThatThrows_IsTreatedAsUnsupported()
    {
        var monitor = new LinuxGpuMonitor([new ThrowingSource()], Logger);

        var gpu = monitor.Sample(null);

        Assert.True(gpu.IsAvailable);
        Assert.Null(gpu.UsagePercent);
    }

    [Fact]
    public void Nvml_ReportsUtilizationAndMemory_AndSuppressesDuplicateDrmCard()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x10de", "0x2684");  // the same NVIDIA GPU, seen through DRM
        AddCard(root, "card1", "0x8086", "0x9a49");
        var nvml = new FakeNvml { Name = "NVIDIA GeForce RTX 4090", Utilization = 88, Total = 24UL << 30, Used = 5UL << 30 };

        var monitor = LinuxGpuMonitor.Discover(root.Files, Logger, () => nvml);
        var gpu = monitor.Sample(null);

        Assert.Equal(2, monitor.Adapters.Count);  // NVML GPU + Intel; the NVIDIA DRM card isn't listed twice
        Assert.Equal("NVIDIA GeForce RTX 4090", gpu.Name);
        Assert.Equal(88.0, gpu.UsagePercent);
        Assert.Equal(5UL << 30, gpu.UsedMemoryBytes);
        Assert.Equal(24UL << 30, gpu.TotalMemoryBytes);
    }

    [Fact]
    public void NvidiaWithoutNvml_StaysListedButUnsupported()
    {
        using var root = new FixtureRoot();
        AddCard(root, "card0", "0x10de", "0x2684");

        var monitor = LinuxGpuMonitor.Discover(root.Files, Logger, () => null);
        var gpu = monitor.Sample(null);

        Assert.Single(monitor.Adapters);
        Assert.Null(gpu.UsagePercent);
        Assert.StartsWith("NVIDIA GPU", gpu.Name);
    }

    [Fact]
    public void Nvml_FailingQueries_YieldNullsNotZeros()
    {
        var source = new NvmlGpuSource(new FakeNvml { Fail = true }, 0);

        var reading = source.Read();

        Assert.Null(reading.UsagePercent);
        Assert.Null(reading.TotalBytes);
    }

    [Fact]
    public void Dispose_IsIdempotent_SoNvmlIsShutDownOnlyOnce()
    {
        // SystemMonitorService and the DI container both dispose the GPU monitor at exit; a second
        // nvmlShutdown/dlclose on the already-freed library would crash the process.
        using var root = new FixtureRoot();
        var nvml = new FakeNvml();
        var monitor = LinuxGpuMonitor.Discover(root.Files, Logger, () => nvml);

        monitor.Dispose();
        monitor.Dispose();

        Assert.Equal(1, nvml.DisposeCount);
    }

    [Fact]
    public void NvmlLibrary_IsNotRequired()
    {
        // Off Linux (and on Linux without NVIDIA's driver) the library can't load: that must be "no NVML", never an exception.
        if (!OperatingSystem.IsLinux())
        {
            Assert.Null(NvmlApi.TryCreate(Logger));
        }
        else
        {
            var exception = Record.Exception(() => NvmlApi.TryCreate(Logger)?.Dispose());
            Assert.Null(exception);
        }
    }

    private sealed class ThrowingSource : IGpuAdapterSource
    {
        public string Id => "bad";
        public string Name => "Bad GPU";
        public GpuReading Read() => throw new InvalidOperationException("boom");
    }

    private sealed class FakeNvml : INvmlApi
    {
        public string Name { get; init; } = "Fake";
        public double Utilization { get; init; }
        public ulong Total { get; init; }
        public ulong Used { get; init; }
        public bool Fail { get; init; }
        public int DisposeCount { get; private set; }

        public int DeviceCount => 1;
        public string? GetName(int index) => Name;

        public bool TryGetUtilization(int index, out double gpuPercent)
        {
            gpuPercent = Utilization;
            return !Fail;
        }

        public bool TryGetMemory(int index, out ulong totalBytes, out ulong usedBytes)
        {
            totalBytes = Total;
            usedBytes = Used;
            return !Fail;
        }

        public void Dispose() => DisposeCount++;
    }
}
