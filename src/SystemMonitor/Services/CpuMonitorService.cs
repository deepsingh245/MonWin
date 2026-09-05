using System.Diagnostics;
using Microsoft.Win32;
using SystemMonitor.Models;
using SystemMonitor.Native;

namespace SystemMonitor.Services;

public sealed class CpuMonitorService : ICpuMonitor
{
    private readonly ILoggingService _logger;
    private readonly string _processorName;
    private readonly int _logicalCoreCount = Environment.ProcessorCount;
    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevSample;

    public CpuMonitorService(ILoggingService logger)
    {
        _logger = logger;
        _processorName = ReadProcessorName();
    }

    public CpuMetrics Sample()
    {
        try
        {
            if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                return CpuMetrics.Unavailable with { ProcessorName = _processorName, LogicalCoreCount = _logicalCoreCount };
            }

            var idleTicks = idle.ToUInt64();
            var kernelTicks = kernel.ToUInt64();
            var userTicks = user.ToUInt64();

            double usage = 0.0;
            if (_hasPrevSample)
            {
                usage = CpuUsageCalculator.Calculate(_prevIdle, _prevKernel, _prevUser, idleTicks, kernelTicks, userTicks);
            }

            _prevIdle = idleTicks;
            _prevKernel = kernelTicks;
            _prevUser = userTicks;
            _hasPrevSample = true;

            return new CpuMetrics
            {
                IsAvailable = true,
                UsagePercent = usage,
                FrequencyMhz = TryReadFrequencyMhz(),
                ProcessorName = _processorName,
                LogicalCoreCount = _logicalCoreCount,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"CPU sampling failed: {ex.Message}");
            return CpuMetrics.Unavailable with { ProcessorName = _processorName, LogicalCoreCount = _logicalCoreCount };
        }
    }

    private double? TryReadFrequencyMhz()
    {
        try
        {
            var size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ProcessorPowerInformation>() * _logicalCoreCount;
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                var result = NativeMethods.CallNtPowerInformation(NativeMethods.ProcessorInformation, 0, 0, buffer, (uint)size);
                if (result != 0)
                {
                    return null;
                }

                ulong total = 0;
                var structSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ProcessorPowerInformation>();
                for (var i = 0; i < _logicalCoreCount; i++)
                {
                    var ptr = nint.Add(buffer, i * structSize);
                    var info = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.ProcessorPowerInformation>(ptr);
                    total += info.CurrentMhz;
                }

                return _logicalCoreCount > 0 ? (double)total / _logicalCoreCount : null;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string ReadProcessorName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "Unknown Processor";
        }
        catch
        {
            return "Unknown Processor";
        }
    }
}
