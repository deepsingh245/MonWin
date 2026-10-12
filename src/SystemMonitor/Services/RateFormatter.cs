using System.Globalization;

namespace SystemMonitor.Services;

/// <summary>
/// Formats byte rates compactly so the overlay text width stays stable. Always uses the
/// invariant culture ("1.2", never "1,2") for the same reason.
/// </summary>
public static class RateFormatter
{
    private static readonly string[] ByteUnits = ["B/s", "KB/s", "MB/s", "GB/s"];
    private static readonly string[] BitUnits = ["bps", "Kbps", "Mbps", "Gbps"];

    /// <param name="bytesPerSecond">The rate, always supplied in bytes per second.</param>
    /// <param name="bits">True to show bits (×8, 1000 base, as ISPs advertise); false for bytes (1024 base).</param>
    public static string Format(double bytesPerSecond, bool bits = false)
    {
        var value = Math.Max(0, bytesPerSecond) * (bits ? 8 : 1);
        var step = bits ? 1000.0 : 1024.0;
        var units = bits ? BitUnits : ByteUnits;

        var unit = 0;
        while (value >= step && unit < units.Length - 1)
        {
            value /= step;
            unit++;
        }

        // One decimal below 10 (e.g. "1.2 MB/s"), none above ("85 KB/s") — max 4 significant chars.
        var format = unit == 0 || value >= 10 ? "F0" : "F1";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {units[unit]}";
    }
}
