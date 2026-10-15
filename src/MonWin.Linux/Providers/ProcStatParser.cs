using System.Globalization;

namespace MonWin.Linux.Providers;

/// <summary>Cumulative CPU time in clock ticks, mapped to the convention <c>CpuUsageCalculator</c> expects.</summary>
/// <param name="Idle">idle + iowait.</param>
/// <param name="Kernel">system + irq + softirq PLUS idle + iowait — like Windows' GetSystemTimes, kernel time includes idle.</param>
/// <param name="User">user + nice + steal.</param>
internal readonly record struct CpuTicks(ulong Idle, ulong Kernel, ulong User);

/// <summary>
/// Parses the aggregate "cpu" line of /proc/stat:
/// <c>cpu  user nice system idle iowait irq softirq steal guest guest_nice</c>.
/// guest and guest_nice are already counted inside user/nice by the kernel, so they are ignored.
/// </summary>
internal static class ProcStatParser
{
    public static bool TryParse(string? text, out CpuTicks ticks)
    {
        ticks = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            // Aggregate line is exactly "cpu" followed by whitespace; "cpu0", "cpu1"... are per-core.
            if (!line.StartsWith("cpu", StringComparison.Ordinal) || line.Length < 4 || !char.IsWhiteSpace(line[3]))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            // Old kernels (< 2.6.11) report fewer columns; we require the first four
            // (user, nice, system, idle) and treat any missing later column as zero.
            if (parts.Length < 5)
            {
                return false;
            }

            var values = new ulong[8];
            for (var i = 0; i < values.Length && i + 1 < parts.Length; i++)
            {
                if (!ulong.TryParse(parts[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                {
                    return false;
                }
            }

            ulong user = values[0], nice = values[1], system = values[2], idle = values[3];
            ulong iowait = values[4], irq = values[5], softirq = values[6], steal = values[7];

            var idleTotal = idle + iowait;
            ticks = new CpuTicks(
                Idle: idleTotal,
                Kernel: system + irq + softirq + idleTotal,
                User: user + nice + steal);
            return true;
        }

        return false;
    }
}
