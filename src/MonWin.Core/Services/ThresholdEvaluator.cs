using SystemMonitor.Models;

namespace SystemMonitor.Services;

public static class ThresholdEvaluator
{
    /// <summary>
    /// Critical at or above <paramref name="crit"/>, Warning at or above <paramref name="warn"/>.
    /// An unavailable reading (null) is always Normal — "N/A" should never flash red.
    /// </summary>
    public static MetricSeverity Evaluate(double? value, int warn, int crit)
    {
        if (value is not { } v)
        {
            return MetricSeverity.Normal;
        }

        if (v >= crit)
        {
            return MetricSeverity.Critical;
        }

        return v >= warn ? MetricSeverity.Warning : MetricSeverity.Normal;
    }

    /// <summary>Clamps a warn/crit pair to 1–100 with warn ≤ crit.</summary>
    public static (int Warn, int Crit) Normalize(int warn, int crit)
    {
        crit = Math.Clamp(crit, 1, 100);
        warn = Math.Clamp(warn, 1, crit);
        return (warn, crit);
    }
}
