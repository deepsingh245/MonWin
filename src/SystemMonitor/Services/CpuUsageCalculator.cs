namespace SystemMonitor.Services;

/// <summary>
/// Pure delta-based CPU usage math, isolated from the Win32 call so it can be unit
/// tested without touching GetSystemTimes.
/// </summary>
public static class CpuUsageCalculator
{
    /// <summary>
    /// Computes % CPU busy between two GetSystemTimes samples. Idle/kernel/user are
    /// cumulative 100ns tick counts as returned by the Win32 API (kernel time includes
    /// idle time, per the documented GetSystemTimes contract).
    /// </summary>
    public static double Calculate(ulong prevIdle, ulong prevKernel, ulong prevUser, ulong curIdle, ulong curKernel, ulong curUser)
    {
        // Handle counter wrap-around / bogus reversed samples defensively.
        if (curKernel < prevKernel || curUser < prevUser || curIdle < prevIdle)
        {
            return 0.0;
        }

        var idleDelta = curIdle - prevIdle;
        var kernelDelta = curKernel - prevKernel;
        var userDelta = curUser - prevUser;
        var totalDelta = kernelDelta + userDelta;

        if (totalDelta == 0)
        {
            return 0.0;
        }

        var busyFraction = 1.0 - (double)idleDelta / totalDelta;
        return Clamp(busyFraction * 100.0);
    }

    public static double Clamp(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent))
        {
            return 0.0;
        }

        return Math.Clamp(percent, 0.0, 100.0);
    }
}
