using System.Diagnostics;
using SystemMonitor.Services;

namespace MonWin.Linux.Providers;

/// <summary>
/// Disk activity from /proc/diskstats deltas. Read/write speed is summed over all physical
/// disks; "busy" is the busiest single disk, because averaging a saturated disk with an idle
/// one would hide the saturation (this matches how Task Manager presents per-disk activity).
/// </summary>
internal sealed class LinuxDiskDataSource : IDiskDataSource
{
    private const double SectorBytes = 512.0;

    private readonly LinuxFiles _files;
    private readonly Func<double> _clockSeconds;
    private readonly Dictionary<string, bool> _isWholeDevice = new();
    private Dictionary<string, DiskCounters> _previous = new();
    private double _previousAt;
    private bool _hasPrevious;

    public LinuxDiskDataSource(LinuxFiles files, Func<double>? clockSeconds = null)
    {
        _files = files;
        var stopwatch = Stopwatch.StartNew();
        _clockSeconds = clockSeconds ?? (() => stopwatch.Elapsed.TotalSeconds);
    }

    public bool TryRead(out float idlePercent, out float readBytesPerSecond, out float writeBytesPerSecond)
    {
        idlePercent = 100f;
        readBytesPerSecond = writeBytesPerSecond = 0f;

        var text = _files.ReadText("/proc/diskstats");
        if (text is null)
        {
            return false;
        }

        var current = DiskStatsParser.Parse(text)
            .Where(d => IsWholeDevice(d.Name))
            .ToDictionary(d => d.Name);
        var now = _clockSeconds();

        if (_hasPrevious && now > _previousAt)
        {
            var elapsed = now - _previousAt;
            double read = 0, write = 0, busiest = 0;
            foreach (var (name, cur) in current)
            {
                // A device that appeared since the last sample has no baseline yet; counters
                // that went backwards (device reset) are skipped rather than reported negative.
                if (!_previous.TryGetValue(name, out var prev)
                    || cur.SectorsRead < prev.SectorsRead
                    || cur.SectorsWritten < prev.SectorsWritten
                    || cur.IoTicksMs < prev.IoTicksMs)
                {
                    continue;
                }

                read += (cur.SectorsRead - prev.SectorsRead) * SectorBytes / elapsed;
                write += (cur.SectorsWritten - prev.SectorsWritten) * SectorBytes / elapsed;
                busiest = Math.Max(busiest, (cur.IoTicksMs - prev.IoTicksMs) / (elapsed * 1000.0) * 100.0);
            }

            readBytesPerSecond = (float)read;
            writeBytesPerSecond = (float)write;
            idlePercent = (float)(100.0 - Math.Clamp(busiest, 0, 100));
        }

        _previous = current;
        _previousAt = now;
        _hasPrevious = true;
        return true;
    }

    // Which devices are whole disks doesn't change while running (hot-plugged disks are new
    // names), so it is decided once per name instead of probing /sys/block on every poll.
    private bool IsWholeDevice(string name)
    {
        if (!_isWholeDevice.TryGetValue(name, out var whole))
        {
            whole = DiskStatsParser.IsWholePhysicalDevice(name, n => _files.DirectoryExists($"/sys/block/{n}"));
            _isWholeDevice[name] = whole;
        }

        return whole;
    }
}
