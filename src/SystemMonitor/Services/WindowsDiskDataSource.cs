using System.Diagnostics;

namespace SystemMonitor.Services;

/// <summary>
/// Live implementation of <see cref="IDiskDataSource"/> backed by the PhysicalDisk
/// "_Total" performance counters. Rate counters need two samples to be meaningful, so the
/// instances are created once and reused across polls.
/// </summary>
public sealed class WindowsDiskDataSource : IDiskDataSource, IDisposable
{
    private const string Category = "PhysicalDisk";
    private const string TotalInstance = "_Total";

    private readonly ILoggingService _logger;
    private PerformanceCounter? _idleCounter;
    private PerformanceCounter? _readCounter;
    private PerformanceCounter? _writeCounter;
    private bool _unavailable;

    public WindowsDiskDataSource(ILoggingService logger)
    {
        _logger = logger;
    }

    public bool TryRead(out float idlePercent, out float readBytesPerSecond, out float writeBytesPerSecond)
    {
        idlePercent = readBytesPerSecond = writeBytesPerSecond = 0f;

        if (!EnsureCounters())
        {
            return false;
        }

        try
        {
            idlePercent = _idleCounter!.NextValue();
            readBytesPerSecond = _readCounter!.NextValue();
            writeBytesPerSecond = _writeCounter!.NextValue();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Disk counter NextValue failed: {ex.Message}");
            return false;
        }
    }

    private bool EnsureCounters()
    {
        if (_idleCounter is not null)
        {
            return true;
        }

        // Once the category is known to be missing (counters disabled/corrupted), stop
        // retrying every tick — the check itself is slow and would just log-spam.
        if (_unavailable)
        {
            return false;
        }

        try
        {
            if (!PerformanceCounterCategory.Exists(Category))
            {
                _logger.LogWarning("PhysicalDisk performance counter category not found; disk metrics unavailable.");
                _unavailable = true;
                return false;
            }

            _idleCounter = new PerformanceCounter(Category, "% Idle Time", TotalInstance, readOnly: true);
            _readCounter = new PerformanceCounter(Category, "Disk Read Bytes/sec", TotalInstance, readOnly: true);
            _writeCounter = new PerformanceCounter(Category, "Disk Write Bytes/sec", TotalInstance, readOnly: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Disk counter setup failed: {ex.Message}");
            Dispose();
            _unavailable = true;
            return false;
        }
    }

    public void Dispose()
    {
        _idleCounter?.Dispose();
        _readCounter?.Dispose();
        _writeCounter?.Dispose();
        _idleCounter = _readCounter = _writeCounter = null;
    }
}
