namespace SystemMonitor.Models;

/// <summary>How a metric's current value compares to its user-configured thresholds.</summary>
public enum MetricSeverity
{
    Normal,
    Warning,
    Critical,
}
