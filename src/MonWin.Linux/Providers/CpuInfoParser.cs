using System.Globalization;

namespace MonWin.Linux.Providers;

/// <summary>Extracts the processor name and per-core clock from /proc/cpuinfo.</summary>
internal static class CpuInfoParser
{
    public static string? ParseModelName(string? text)
    {
        // x86 uses "model name"; ARM kernels use "Model" (board) or "Hardware" (SoC) instead.
        // Prefer them in that order regardless of where they appear in the file.
        string[] preference = ["model name", "Model", "Hardware"];
        var fields = Fields(text).Where(f => f.Value.Length > 0).ToList();
        foreach (var key in preference)
        {
            if (fields.FirstOrDefault(f => f.Key == key) is { Key: not null } match)
            {
                return CollapseSpaces(match.Value);
            }
        }

        return null;
    }

    /// <summary>Mean of every "cpu MHz" line, or null when the file has none (common on ARM).</summary>
    public static double? ParseAverageMhz(string? text)
    {
        double sum = 0;
        var count = 0;
        foreach (var (key, value) in Fields(text))
        {
            if (key == "cpu MHz"
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)
                && mhz > 0)
            {
                sum += mhz;
                count++;
            }
        }

        return count == 0 ? null : sum / count;
    }

    private static IEnumerable<(string Key, string Value)> Fields(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                yield return (line[..colon].Trim(), line[(colon + 1)..].Trim());
            }
        }
    }

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
