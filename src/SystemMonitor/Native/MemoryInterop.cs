using System.Runtime.InteropServices;

namespace SystemMonitor.Native;

/// <summary>P/Invoke wrapper around GlobalMemoryStatusEx (kernel32.dll) — the standard, documented,
/// user-mode API for physical/virtual memory statistics.</summary>
internal static partial class MemoryInterop
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MemoryStatusEx
    {
        public uint DwLength;
        public uint DwMemoryLoad;
        public ulong UllTotalPhys;
        public ulong UllAvailPhys;
        public ulong UllTotalPageFile;
        public ulong UllAvailPageFile;
        public ulong UllTotalVirtual;
        public ulong UllAvailVirtual;
        public ulong UllAvailExtendedVirtual;
    }

    /// <summary>Returns null if the underlying Win32 call fails.</summary>
    internal static MemoryStatusEx? TryGetMemoryStatus()
    {
        var status = new MemoryStatusEx { DwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? status : null;
    }
}
