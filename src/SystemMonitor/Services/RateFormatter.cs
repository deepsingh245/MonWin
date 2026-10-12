namespace SystemMonitor.Services;

/// <summary>Formats byte rates compactly (1024 base) so the overlay text width stays stable.</summary>
public static class RateFormatter
{
    private static readonly string[] Units = ["B/s", "KB/s", "MB/s", "GB/s"];

    public static string Format(double bytesPerSecond)
    {
        var value = Math.Max(0, bytesPerSecond);
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // One decimal below 10 (e.g. "1.2 MB/s"), none above ("85 KB/s") — max 4 significant chars.
        var number = unit == 0 || value >= 10 ? $"{value:F0}" : $"{value:F1}";
        return $"{number} {Units[unit]}";
    }
}
