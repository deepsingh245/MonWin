using System.Globalization;

namespace MonWin.Linux.Providers;

/// <summary>Cumulative counters for one block device from /proc/diskstats.</summary>
/// <param name="Name">Kernel device name, e.g. "nvme0n1".</param>
/// <param name="SectorsRead">512-byte sectors (the kernel always reports 512-byte units here).</param>
/// <param name="SectorsWritten">512-byte sectors.</param>
/// <param name="IoTicksMs">Milliseconds the device had I/O in flight ("io_ticks"), the basis of "% busy".</param>
internal readonly record struct DiskCounters(string Name, ulong SectorsRead, ulong SectorsWritten, ulong IoTicksMs);

internal static class DiskStatsParser
{
    // Fields after the device name: 0 reads, 1 reads merged, 2 sectors read, 3 ms reading,
    // 4 writes, 5 writes merged, 6 sectors written, 7 ms writing, 8 in flight, 9 io_ticks ms.
    private const int FieldSectorsRead = 2;
    private const int FieldSectorsWritten = 6;
    private const int FieldIoTicks = 9;
    private const int MinFields = 10;

    public static IReadOnlyList<DiskCounters> Parse(string? text)
    {
        var result = new List<DiskCounters>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        foreach (var line in text.Split('\n'))
        {
            // major minor name field0 field1 ...
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 + MinFields)
            {
                continue;
            }

            var counters = parts.Skip(3).Take(MinFields)
                .Select(p => ulong.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? (ulong?)v : null)
                .ToArray();
            if (counters[FieldSectorsRead] is not { } read
                || counters[FieldSectorsWritten] is not { } written
                || counters[FieldIoTicks] is not { } ticks)
            {
                continue;
            }

            result.Add(new DiskCounters(parts[2], read, written, ticks));
        }

        return result;
    }

    /// <summary>
    /// True for a physical-ish whole device. Partitions (they double-count their parent),
    /// loop/ram/zram devices, device-mapper and md layers (they double-count the disks
    /// underneath) and optical/floppy drives are excluded. <paramref name="isBlockRoot"/>
    /// tells whether /sys/block/NAME exists; sysfs lists only whole devices there.
    /// </summary>
    public static bool IsWholePhysicalDevice(string name, Func<string, bool> isBlockRoot)
    {
        string[] ignoredPrefixes = ["loop", "ram", "zram", "dm-", "md", "sr", "fd", "nbd"];
        return !ignoredPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)) && isBlockRoot(name);
    }
}
