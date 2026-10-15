using System.Runtime.InteropServices;
using System.Text;
using SystemMonitor.Services;

namespace MonWin.Linux.Providers;

/// <summary>The few NVML (NVIDIA Management Library) queries MonWin needs, behind an interface so GPU logic is testable without NVIDIA hardware.</summary>
internal interface INvmlApi : IDisposable
{
    int DeviceCount { get; }

    string? GetName(int index);

    bool TryGetUtilization(int index, out double gpuPercent);

    bool TryGetMemory(int index, out ulong totalBytes, out ulong usedBytes);
}

/// <summary>
/// NVIDIA GPU readings through libnvidia-ml, the user-space library that ships with the
/// proprietary driver (the same one nvidia-smi uses). It is loaded at runtime with dlopen
/// semantics, so MonWin has no build- or install-time dependency on it: if the library is
/// absent (no NVIDIA driver, open-source nouveau driver, container) <see cref="TryCreate"/>
/// returns null and NVIDIA readings are simply unsupported. No root and no extra process.
/// </summary>
internal sealed unsafe class NvmlApi : INvmlApi
{
    private const int Success = 0;
    private static readonly string[] LibraryNames = ["libnvidia-ml.so.1", "libnvidia-ml.so"];

    // Serializes queries against Dispose: freeing the library while a poll is calling into it would crash.
    private readonly object _gate = new();
    private bool _disposed;
    private readonly nint _library;
    private readonly nint[] _handles;
    private readonly delegate* unmanaged<nint, byte*, uint, int> _getName;
    private readonly delegate* unmanaged<nint, NvmlUtilization*, int> _getUtilization;
    private readonly delegate* unmanaged<nint, NvmlMemory*, int> _getMemory;
    private readonly delegate* unmanaged<int> _shutdown;

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlUtilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlMemory
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    private NvmlApi(
        nint library,
        nint[] handles,
        delegate* unmanaged<nint, byte*, uint, int> getName,
        delegate* unmanaged<nint, NvmlUtilization*, int> getUtilization,
        delegate* unmanaged<nint, NvmlMemory*, int> getMemory,
        delegate* unmanaged<int> shutdown)
    {
        _library = library;
        _handles = handles;
        _getName = getName;
        _getUtilization = getUtilization;
        _getMemory = getMemory;
        _shutdown = shutdown;
    }

    public int DeviceCount => _handles.Length;

    /// <summary>Returns null (never throws) when NVML can't be loaded or initialized, or there are no devices.</summary>
    public static NvmlApi? TryCreate(ILoggingService logger)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        nint library = 0;
        try
        {
            foreach (var name in LibraryNames)
            {
                if (NativeLibrary.TryLoad(name, out library))
                {
                    break;
                }
            }

            if (library == 0)
            {
                return null;
            }

            if (!NativeLibrary.TryGetExport(library, "nvmlInit_v2", out var init)
                || !NativeLibrary.TryGetExport(library, "nvmlShutdown", out var shutdown)
                || !NativeLibrary.TryGetExport(library, "nvmlDeviceGetCount_v2", out var getCount)
                || !NativeLibrary.TryGetExport(library, "nvmlDeviceGetHandleByIndex_v2", out var getHandle)
                || !NativeLibrary.TryGetExport(library, "nvmlDeviceGetName", out var getName)
                || !NativeLibrary.TryGetExport(library, "nvmlDeviceGetUtilizationRates", out var getUtilization)
                || !NativeLibrary.TryGetExport(library, "nvmlDeviceGetMemoryInfo", out var getMemory))
            {
                logger.LogWarning("libnvidia-ml was found but is missing expected functions; NVIDIA GPU readings disabled.");
                NativeLibrary.Free(library);
                return null;
            }

            if (((delegate* unmanaged<int>)init)() != Success)
            {
                logger.LogInfo("NVML initialization failed (no usable NVIDIA driver); NVIDIA GPU readings disabled.");
                NativeLibrary.Free(library);
                return null;
            }

            uint count = 0;
            if (((delegate* unmanaged<uint*, int>)getCount)(&count) != Success || count == 0)
            {
                ((delegate* unmanaged<int>)shutdown)();
                NativeLibrary.Free(library);
                return null;
            }

            var handles = new List<nint>();
            for (uint i = 0; i < count; i++)
            {
                nint handle = 0;
                if (((delegate* unmanaged<uint, nint*, int>)getHandle)(i, &handle) == Success)
                {
                    handles.Add(handle);
                }
            }

            if (handles.Count == 0)
            {
                ((delegate* unmanaged<int>)shutdown)();
                NativeLibrary.Free(library);
                return null;
            }

            return new NvmlApi(
                library,
                handles.ToArray(),
                (delegate* unmanaged<nint, byte*, uint, int>)getName,
                (delegate* unmanaged<nint, NvmlUtilization*, int>)getUtilization,
                (delegate* unmanaged<nint, NvmlMemory*, int>)getMemory,
                (delegate* unmanaged<int>)shutdown);
        }
        catch (Exception ex)
        {
            logger.LogWarning($"NVML probing failed: {ex.Message}");
            if (library != 0)
            {
                NativeLibrary.Free(library);
            }

            return null;
        }
    }

    public string? GetName(int index)
    {
        lock (_gate)
        {
            if (_disposed || (uint)index >= (uint)_handles.Length)
            {
                return null;
            }

            var buffer = stackalloc byte[96];
            if (_getName(_handles[index], buffer, 96) != Success)
            {
                return null;
            }

            var length = 0;
            while (length < 96 && buffer[length] != 0)
            {
                length++;
            }

            return length == 0 ? null : Encoding.UTF8.GetString(buffer, length);
        }
    }

    public bool TryGetUtilization(int index, out double gpuPercent)
    {
        gpuPercent = 0;
        lock (_gate)
        {
            if (_disposed || (uint)index >= (uint)_handles.Length)
            {
                return false;
            }

            NvmlUtilization utilization;
            if (_getUtilization(_handles[index], &utilization) != Success)
            {
                return false;
            }

            gpuPercent = Math.Clamp((double)utilization.Gpu, 0, 100);
            return true;
        }
    }

    public bool TryGetMemory(int index, out ulong totalBytes, out ulong usedBytes)
    {
        totalBytes = usedBytes = 0;
        lock (_gate)
        {
            if (_disposed || (uint)index >= (uint)_handles.Length)
            {
                return false;
            }

            NvmlMemory memory;
            if (_getMemory(_handles[index], &memory) != Success)
            {
                return false;
            }

            totalBytes = memory.Total;
            usedBytes = memory.Used;
            return true;
        }
    }

    /// <summary>Safe to call more than once (the monitor and the DI container both dispose it).</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _shutdown();
            }
            finally
            {
                NativeLibrary.Free(_library);
            }
        }
    }
}
