using System.Runtime.InteropServices;

namespace SystemMonitor.Native;

/// <summary>
/// P/Invoke surface for CPU timing and frequency. Every function here is a documented,
/// public, non-privileged Win32 API — no driver, no admin rights required.
/// </summary>
internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [LibraryImport("powrprof.dll")]
    internal static partial int CallNtPowerInformation(
        int informationLevel,
        nint lpInputBuffer,
        uint nInputBufferSize,
        nint lpOutputBuffer,
        uint nOutputBufferSize);

    internal const int ProcessorInformation = 11; // POWER_INFORMATION_LEVEL.ProcessorInformation

    [StructLayout(LayoutKind.Sequential)]
    internal struct FILETIME
    {
        public uint DwLowDateTime;
        public uint DwHighDateTime;

        public readonly ulong ToUInt64() => ((ulong)DwHighDateTime << 32) | DwLowDateTime;
    }

    /// <summary>Matches PROCESSOR_POWER_INFORMATION from powrprof.h (one instance per logical core).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }
}
