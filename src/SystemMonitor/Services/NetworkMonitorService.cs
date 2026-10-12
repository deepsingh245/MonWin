using System.Diagnostics;
using System.Net.NetworkInformation;
using SystemMonitor.Models;

namespace SystemMonitor.Services;

/// <summary>
/// Computes upload/download rates from the cumulative byte counters that
/// <see cref="NetworkInterface.GetIPStatistics"/> exposes — no performance counters needed.
/// Like CPU, the first sample only primes the baseline and reports 0.
/// </summary>
public sealed class NetworkMonitorService : INetworkMonitor
{
    private readonly ILoggingService _logger;
    private readonly Stopwatch _stopwatch = new();
    private long _prevReceived, _prevSent;
    private bool _hasPrevSample;

    public NetworkMonitorService(ILoggingService logger)
    {
        _logger = logger;
    }

    public NetworkMetrics Sample()
    {
        try
        {
            long received = 0, sent = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up
                    || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var stats = nic.GetIPStatistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }

            var elapsedSec = _stopwatch.Elapsed.TotalSeconds;
            double download = 0, upload = 0;
            if (_hasPrevSample)
            {
                download = RatePerSecond(_prevReceived, received, elapsedSec);
                upload = RatePerSecond(_prevSent, sent, elapsedSec);
            }

            _prevReceived = received;
            _prevSent = sent;
            _hasPrevSample = true;
            _stopwatch.Restart();

            return new NetworkMetrics
            {
                IsAvailable = true,
                DownloadBytesPerSecond = download,
                UploadBytesPerSecond = upload,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Network sampling failed: {ex.Message}");
            return NetworkMetrics.Unavailable;
        }
    }

    /// <summary>
    /// Byte-counter delta per second. Clamped to 0 because the summed total drops when an
    /// adapter disconnects (its counters leave the sum) — that must not read as a negative rate.
    /// </summary>
    internal static double RatePerSecond(long previous, long current, double elapsedSeconds)
    {
        if (elapsedSeconds <= 0 || current <= previous)
        {
            return 0;
        }

        return (current - previous) / elapsedSeconds;
    }
}
