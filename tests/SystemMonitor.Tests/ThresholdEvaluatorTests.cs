using SystemMonitor.Models;
using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class ThresholdEvaluatorTests
{
    [Theory]
    [InlineData(10.0, MetricSeverity.Normal)]
    [InlineData(74.9, MetricSeverity.Normal)]
    [InlineData(75.0, MetricSeverity.Warning)]
    [InlineData(89.9, MetricSeverity.Warning)]
    [InlineData(90.0, MetricSeverity.Critical)]
    [InlineData(100.0, MetricSeverity.Critical)]
    public void Evaluate_ClassifiesAgainstWarnAndCrit(double value, MetricSeverity expected)
    {
        Assert.Equal(expected, ThresholdEvaluator.Evaluate(value, 75, 90));
    }

    [Fact]
    public void Evaluate_UnavailableValue_IsNormal()
    {
        Assert.Equal(MetricSeverity.Normal, ThresholdEvaluator.Evaluate(null, 1, 1));
    }

    [Theory]
    [InlineData(95, 80, 80, 80)]   // warn above crit → pulled down to crit
    [InlineData(-5, 150, 1, 100)]  // both out of range → clamped
    [InlineData(0, 0, 1, 1)]       // zero would make everything "critical" → minimum 1
    [InlineData(60, 85, 60, 85)]   // valid pair untouched
    public void Normalize_ClampsAndOrders(int warn, int crit, int expectedWarn, int expectedCrit)
    {
        Assert.Equal((expectedWarn, expectedCrit), ThresholdEvaluator.Normalize(warn, crit));
    }
}
