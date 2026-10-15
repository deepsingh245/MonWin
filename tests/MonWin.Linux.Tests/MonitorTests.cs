using MonWin.Linux.Providers;
using SystemMonitor.Tests;
using Xunit;

namespace MonWin.Linux.Tests;

public class MonitorTests
{
    private static string Stat(ulong user, ulong system, ulong idle) =>
        $"cpu  {user} 0 {system} {idle} 0 0 0 0 0 0\ncpu0 {user} 0 {system} {idle} 0 0 0 0 0 0\n";

    [Fact]
    public void Cpu_FirstSampleIsZero_ThenUsesDeltas()
    {
        using var root = new FixtureRoot();
        root.Write("/proc/cpuinfo", "model name\t: Test CPU @ 3GHz\n");
        root.Write("/proc/stat", Stat(user: 100, system: 100, idle: 800));
        var monitor = new LinuxCpuMonitor(root.Files, new NoOpLoggingService());

        var first = monitor.Sample();
        root.Write("/proc/stat", Stat(user: 150, system: 150, idle: 850)); // +50 user, +50 system, +50 idle
        var second = monitor.Sample();

        Assert.True(first.IsAvailable);
        Assert.Equal(0.0, first.UsagePercent);
        Assert.Equal(100.0 * 100 / 150, second.UsagePercent, 3);
        Assert.Equal("Test CPU @ 3GHz", second.ProcessorName);
    }

    [Fact]
    public void Cpu_FrequencyIsMeanOfCpufreqInMhz()
    {
        using var root = new FixtureRoot();
        root.Write("/proc/stat", Stat(1, 1, 8));
        root.Write("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq", "3000000\n");
        root.Write("/sys/devices/system/cpu/cpu1/cpufreq/scaling_cur_freq", "1000000\n");
        root.MakeDirectory("/sys/devices/system/cpu/cpufreq"); // not a cpuN directory: must be ignored

        var cpu = new LinuxCpuMonitor(root.Files, new NoOpLoggingService()).Sample();

        Assert.Equal(2000.0, cpu.FrequencyMhz);
    }

    [Fact]
    public void Cpu_NoCpufreq_FallsBackToCpuinfoMhz()
    {
        using var root = new FixtureRoot();
        root.Write("/proc/stat", Stat(1, 1, 8));
        root.Write("/proc/cpuinfo", "cpu MHz\t: 2400.0\n");

        Assert.Equal(2400.0, new LinuxCpuMonitor(root.Files, new NoOpLoggingService()).Sample().FrequencyMhz);
    }

    [Fact]
    public void Cpu_MissingProcStat_IsUnavailableNotZero()
    {
        using var root = new FixtureRoot();

        var cpu = new LinuxCpuMonitor(root.Files, new NoOpLoggingService()).Sample();

        Assert.False(cpu.IsAvailable);
    }

    [Fact]
    public void Cpu_CountersGoingBackwards_DoNotProduceGarbage()
    {
        using var root = new FixtureRoot();
        root.Write("/proc/stat", Stat(500, 500, 5000));
        var monitor = new LinuxCpuMonitor(root.Files, new NoOpLoggingService());
        monitor.Sample();
        root.Write("/proc/stat", Stat(10, 10, 100)); // e.g. after a counter reset

        var cpu = monitor.Sample();

        Assert.True(cpu.IsAvailable);
        Assert.InRange(cpu.UsagePercent, 0, 100);
    }

    [Fact]
    public void Memory_ComputesUsedFromAvailable()
    {
        using var root = new FixtureRoot();
        root.Write("/proc/meminfo", "MemTotal: 16000000 kB\nMemAvailable: 4000000 kB\n");

        var memory = new LinuxMemoryMonitor(root.Files, new NoOpLoggingService()).Sample();

        Assert.True(memory.IsAvailable);
        Assert.Equal(75.0, memory.UsagePercent, 3);
        Assert.Equal(16_000_000UL * 1024, memory.TotalBytes);
        Assert.Equal(12_000_000UL * 1024, memory.UsedBytes);
    }

    [Fact]
    public void Memory_MissingOrCorruptMeminfo_IsUnavailable()
    {
        using var root = new FixtureRoot();
        var monitor = new LinuxMemoryMonitor(root.Files, new NoOpLoggingService());

        Assert.False(monitor.Sample().IsAvailable);

        root.Write("/proc/meminfo", "not a meminfo file");
        Assert.False(monitor.Sample().IsAvailable);
    }

    private static string DiskStats(ulong sectorsRead, ulong sectorsWritten, ulong ioTicks) =>
        $" 259 0 nvme0n1 10 0 {sectorsRead} 5 20 0 {sectorsWritten} 8 0 {ioTicks} 13\n" +
        $" 259 1 nvme0n1p1 10 0 {sectorsRead} 5 20 0 {sectorsWritten} 8 0 {ioTicks} 13\n" + // partition: must not double count
        $"   7 0 loop0 10 0 {sectorsRead} 5 20 0 {sectorsWritten} 8 0 {ioTicks} 13\n";

    [Fact]
    public void Disk_ComputesThroughputAndBusyFromDeltas()
    {
        using var root = new FixtureRoot();
        root.MakeDirectory("/sys/block/nvme0n1");
        root.MakeDirectory("/sys/block/loop0");
        var now = 0.0;
        var source = new LinuxDiskDataSource(root.Files, () => now);

        root.Write("/proc/diskstats", DiskStats(0, 0, 0));
        Assert.True(source.TryRead(out var idle0, out var read0, out var write0));
        Assert.Equal((100f, 0f, 0f), (idle0, read0, write0));

        now = 2.0;
        root.Write("/proc/diskstats", DiskStats(sectorsRead: 4000, sectorsWritten: 2000, ioTicks: 1000));
        Assert.True(source.TryRead(out var idle, out var read, out var write));

        Assert.Equal(4000 * 512 / 2.0, read, 1);   // only nvme0n1 counted
        Assert.Equal(2000 * 512 / 2.0, write, 1);
        Assert.Equal(50.0, idle, 1);               // 1000 ms busy over 2 s
    }

    [Fact]
    public void Disk_BusyIsClampedAndUsesBusiestDisk()
    {
        using var root = new FixtureRoot();
        root.MakeDirectory("/sys/block/sda");
        root.MakeDirectory("/sys/block/sdb");
        var now = 0.0;
        var source = new LinuxDiskDataSource(root.Files, () => now);
        root.Write("/proc/diskstats", "8 0 sda 0 0 0 0 0 0 0 0 0 0 0\n8 16 sdb 0 0 0 0 0 0 0 0 0 0 0\n");
        source.TryRead(out _, out _, out _);

        now = 1.0;
        // sda 100% busy (and slightly over, from timer jitter), sdb idle.
        root.Write("/proc/diskstats", "8 0 sda 0 0 0 0 0 0 0 0 0 1050 0\n8 16 sdb 0 0 0 0 0 0 0 0 0 0 0\n");
        source.TryRead(out var idle, out _, out _);

        Assert.Equal(0f, idle);
    }

    [Fact]
    public void Disk_CounterReset_IsSkippedNotNegative()
    {
        using var root = new FixtureRoot();
        root.MakeDirectory("/sys/block/sda");
        var now = 0.0;
        var source = new LinuxDiskDataSource(root.Files, () => now);
        root.Write("/proc/diskstats", "8 0 sda 0 0 9000 0 0 0 9000 0 0 9000 0\n");
        source.TryRead(out _, out _, out _);

        now = 1.0;
        root.Write("/proc/diskstats", "8 0 sda 0 0 10 0 0 0 10 0 0 10 0\n");
        source.TryRead(out var idle, out var read, out var write);

        Assert.Equal((100f, 0f, 0f), (idle, read, write));
    }

    [Fact]
    public void Disk_NoDiskstats_ReportsUnavailable()
    {
        using var root = new FixtureRoot();

        Assert.False(new LinuxDiskDataSource(root.Files).TryRead(out _, out _, out _));
    }
}
