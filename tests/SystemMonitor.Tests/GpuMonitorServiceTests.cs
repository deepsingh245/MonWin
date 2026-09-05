using SystemMonitor.Native;
using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class GpuMonitorServiceTests
{
    [Fact]
    public void Sample_NoEngineCategory_ReturnsUnavailable()
    {
        var dataSource = new FakeGpuDataSource { HasEngineCategory = false };
        var service = new GpuMonitorService(dataSource, new NoOpLoggingService());

        var result = service.Sample(null);

        Assert.False(result.IsAvailable);
    }

    [Fact]
    public void Sample_NoEngineSamples_ReturnsUnavailable()
    {
        var dataSource = new FakeGpuDataSource { EngineSamples = [] };
        var service = new GpuMonitorService(dataSource, new NoOpLoggingService());

        var result = service.Sample(null);

        Assert.False(result.IsAvailable);
    }

    [Fact]
    public void Aggregate_MultipleEngineTypesOnSameAdapter_TakesMaxNotSum()
    {
        // Two engines on the same adapter running concurrently: 3D at 40%, Copy at 30%.
        // A naive sum would report 70%; the correct aggregate is the max, 40%.
        var samples = new List<GpuEngineSample>
        {
            new("0x0_0x1", "3D", 40f),
            new("0x0_0x1", "Copy", 30f),
        };

        var result = GpuMonitorService.Aggregate(
            samples, new Dictionary<string, ulong>(), selectedAdapterIndex: null,
            adapters: [], luidOrder: ["0x0_0x1"]);

        Assert.True(result.IsAvailable);
        Assert.Equal(40.0, result.UsagePercent);
    }

    [Fact]
    public void Aggregate_AutoSelect_PicksBusiestAdapter()
    {
        var samples = new List<GpuEngineSample>
        {
            new("gpu-a", "3D", 15f),
            new("gpu-b", "3D", 85f),
        };

        var adapters = new List<Models.GpuAdapterInfo>
        {
            new(0, "gpu-a", "Integrated GPU", null),
            new(1, "gpu-b", "Discrete GPU", 8_000_000_000UL),
        };

        var result = GpuMonitorService.Aggregate(
            samples, new Dictionary<string, ulong>(), selectedAdapterIndex: null,
            adapters, luidOrder: ["gpu-a", "gpu-b"]);

        Assert.Equal("Discrete GPU", result.Name);
        Assert.Equal(85.0, result.UsagePercent);
    }

    [Fact]
    public void Aggregate_ExplicitSelection_UsesThatAdapterRegardlessOfLoad()
    {
        var samples = new List<GpuEngineSample>
        {
            new("gpu-a", "3D", 15f),
            new("gpu-b", "3D", 85f),
        };

        var adapters = new List<Models.GpuAdapterInfo>
        {
            new(0, "gpu-a", "Integrated GPU", null),
            new(1, "gpu-b", "Discrete GPU", 8_000_000_000UL),
        };

        var result = GpuMonitorService.Aggregate(
            samples, new Dictionary<string, ulong>(), selectedAdapterIndex: 0,
            adapters, luidOrder: ["gpu-a", "gpu-b"]);

        Assert.Equal("Integrated GPU", result.Name);
        Assert.Equal(15.0, result.UsagePercent);
    }

    [Fact]
    public void Aggregate_UtilizationOutOfRange_ClampsToValidPercent()
    {
        var samples = new List<GpuEngineSample> { new("gpu-a", "3D", 250f) };

        var result = GpuMonitorService.Aggregate(
            samples, new Dictionary<string, ulong>(), selectedAdapterIndex: null,
            adapters: [], luidOrder: ["gpu-a"]);

        Assert.Equal(100.0, result.UsagePercent);
    }
}
