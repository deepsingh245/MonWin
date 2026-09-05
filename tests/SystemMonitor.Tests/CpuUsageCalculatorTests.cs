using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class CpuUsageCalculatorTests
{
    [Fact]
    public void Calculate_FullyBusy_Returns100()
    {
        // idle doesn't advance while kernel+user do => 100% busy
        var result = CpuUsageCalculator.Calculate(prevIdle: 0, prevKernel: 0, prevUser: 0, curIdle: 0, curKernel: 50, curUser: 50);
        Assert.Equal(100.0, result, 3);
    }

    [Fact]
    public void Calculate_FullyIdle_ReturnsZero()
    {
        var result = CpuUsageCalculator.Calculate(prevIdle: 0, prevKernel: 0, prevUser: 0, curIdle: 100, curKernel: 100, curUser: 0);
        Assert.Equal(0.0, result, 3);
    }

    [Fact]
    public void Calculate_HalfBusy_Returns50()
    {
        var result = CpuUsageCalculator.Calculate(prevIdle: 0, prevKernel: 0, prevUser: 0, curIdle: 50, curKernel: 100, curUser: 0);
        Assert.Equal(50.0, result, 3);
    }

    [Fact]
    public void Calculate_ZeroDelta_ReturnsZero()
    {
        var result = CpuUsageCalculator.Calculate(prevIdle: 10, prevKernel: 20, prevUser: 5, curIdle: 10, curKernel: 20, curUser: 5);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Calculate_ReversedSamples_ReturnsZeroInsteadOfNegativeOrThrowing()
    {
        var result = CpuUsageCalculator.Calculate(prevIdle: 100, prevKernel: 200, prevUser: 50, curIdle: 10, curKernel: 20, curUser: 5);
        Assert.Equal(0.0, result);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-5.0)]
    [InlineData(150.0)]
    public void Clamp_InvalidOrOutOfRangeValues_ClampsToZeroToHundred(double input)
    {
        var result = CpuUsageCalculator.Clamp(input);
        Assert.InRange(result, 0.0, 100.0);
    }

    [Fact]
    public void Clamp_ValidValue_PassesThrough()
    {
        Assert.Equal(42.0, CpuUsageCalculator.Clamp(42.0));
    }
}
