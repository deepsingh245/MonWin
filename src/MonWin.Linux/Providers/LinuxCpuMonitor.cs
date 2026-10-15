using System.Text.RegularExpressions;
using SystemMonitor.Models;
using SystemMonitor.Services;

namespace MonWin.Linux.Providers;

/// <summary>
/// CPU load from deltas of /proc/stat (the same busy-fraction math the Windows monitor
/// uses, via the shared <see cref="CpuUsageCalculator"/>), frequency from cpufreq sysfs.
/// </summary>
internal sealed partial class LinuxCpuMonitor : ICpuMonitor
{
    private readonly LinuxFiles _files;
    private readonly ILoggingService _logger;
    private readonly string _processorName;
    private readonly IReadOnlyList<string> _cpuDirs;
    private CpuTicks _previous;
    private bool _hasPrevious;

    [GeneratedRegex(@"^cpu\d+$")]
    private static partial Regex CpuDirPattern();

    public LinuxCpuMonitor(LinuxFiles files, ILoggingService logger)
    {
        _files = files;
        _logger = logger;
        _processorName = CpuInfoParser.ParseModelName(files.ReadText("/proc/cpuinfo")) ?? "Unknown Processor";
        _cpuDirs = files.ListDirectoryNames("/sys/devices/system/cpu").Where(n => CpuDirPattern().IsMatch(n)).ToList();
    }

    public CpuMetrics Sample()
    {
        var unavailable = CpuMetrics.Unavailable with { ProcessorName = _processorName, LogicalCoreCount = Environment.ProcessorCount };
        try
        {
            if (!ProcStatParser.TryParse(_files.ReadText("/proc/stat"), out var now))
            {
                return unavailable;
            }

            var usage = 0.0;
            if (_hasPrevious)
            {
                usage = CpuUsageCalculator.Calculate(_previous.Idle, _previous.Kernel, _previous.User, now.Idle, now.Kernel, now.User);
            }

            _previous = now;
            _hasPrevious = true;

            return new CpuMetrics
            {
                IsAvailable = true,
                UsagePercent = usage,
                FrequencyMhz = ReadFrequencyMhz(),
                ProcessorName = _processorName,
                LogicalCoreCount = Environment.ProcessorCount,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"CPU sampling failed: {ex.Message}");
            return unavailable;
        }
    }

    private double? ReadFrequencyMhz()
    {
        // scaling_cur_freq is in kHz. Not every system exposes cpufreq (VMs, some ARM boards).
        double sumKhz = 0;
        var count = 0;
        foreach (var dir in _cpuDirs)
        {
            if (_files.ReadUInt64($"/sys/devices/system/cpu/{dir}/cpufreq/scaling_cur_freq") is { } khz && khz > 0)
            {
                sumKhz += khz;
                count++;
            }
        }

        if (count > 0)
        {
            return sumKhz / count / 1000.0;
        }

        return CpuInfoParser.ParseAverageMhz(_files.ReadText("/proc/cpuinfo"));
    }
}
