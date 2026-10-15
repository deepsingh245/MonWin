namespace MonWin.Linux.Views;

/// <summary>
/// The sparkline's plotting math, separate from drawing so it can be unit-tested. Same
/// behavior as the Windows control: auto-scale up to the data's own maximum (never below
/// <c>maxValue</c>), baseline at 0, newest sample at the right edge.
/// </summary>
internal static class SparklineGeometry
{
    /// <summary>Returns one (x, y) per value in control coordinates (y grows downward), or empty when nothing can be drawn.</summary>
    public static (double X, double Y)[] ComputePoints(IReadOnlyList<double>? values, double width, double height, double maxValue)
    {
        if (values is null || values.Count < 2 || width <= 0 || height <= 0)
        {
            return [];
        }

        // NaN/Infinity would poison the min/max (and so every point); plot them as zero instead.
        var clean = values.Select(v => double.IsFinite(v) ? v : 0.0).ToArray();
        var max = Math.Max(maxValue, clean.Max());
        var min = Math.Min(0.0, clean.Min());
        var range = Math.Max(max - min, 0.0001);
        var stepX = width / (clean.Length - 1);

        var points = new (double X, double Y)[clean.Length];
        for (var i = 0; i < clean.Length; i++)
        {
            var normalized = (clean[i] - min) / range;
            points[i] = (i * stepX, height - normalized * height);
        }

        return points;
    }
}
