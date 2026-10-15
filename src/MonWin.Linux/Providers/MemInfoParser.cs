using System.Globalization;

namespace MonWin.Linux.Providers;

/// <summary>Parses /proc/meminfo (values are in kibibytes despite the "kB" label).</summary>
internal static class MemInfoParser
{
    public static bool TryParse(string? text, out ulong totalBytes, out ulong availableBytes)
    {
        totalBytes = availableBytes = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        ulong? total = null, available = null, free = null, buffers = null, cached = null, reclaimable = null;
        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var number = line[(colon + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (number is null || !ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var kib))
            {
                continue;
            }

            var bytes = kib * 1024UL;
            switch (key)
            {
                case "MemTotal": total = bytes; break;
                case "MemAvailable": available = bytes; break;
                case "MemFree": free = bytes; break;
                case "Buffers": buffers = bytes; break;
                case "Cached": cached = bytes; break;
                case "SReclaimable": reclaimable = bytes; break;
            }
        }

        if (total is not { } t || t == 0)
        {
            return false;
        }

        // MemAvailable exists since Linux 3.14. On older kernels, estimate it the way
        // `free` did before: free + buffers + cache (+ reclaimable slab).
        var estimate = (free ?? 0) + (buffers ?? 0) + (cached ?? 0) + (reclaimable ?? 0);
        totalBytes = t;
        availableBytes = Math.Min(available ?? estimate, t);
        return true;
    }
}
