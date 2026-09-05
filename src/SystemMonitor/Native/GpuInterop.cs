using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;

namespace SystemMonitor.Native;

public sealed record GpuEngineSample(string Luid, string EngineType, float UtilizationPercent);

public sealed record WmiAdapterRaw(string Name, ulong? AdapterRamBytes);

/// <summary>
/// Thin wrapper over the "GPU Engine" / "GPU Process Memory" performance counter
/// categories (documented, user-mode, backed by the WDDM scheduler — the same source
/// Task Manager's Performance &gt; GPU tab uses) plus WMI Win32_VideoController for
/// adapter identity. Deliberately avoids DXGI/D3DKMT COM interop to keep the native
/// surface small; no kernel driver, no admin rights required anywhere here.
/// </summary>
internal static class GpuInterop
{
    private const string EngineCategory = "GPU Engine";
    private const string MemoryCategory = "GPU Process Memory";

    // Real-world instance name shape: pid_1234_luid_0x00000000_0x0000C8B5_phys_0_eng_0_engtype_3D
    private static readonly Regex EngineInstanceRegex = new(
        @"luid_(?<luid>0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+).*?engtype_(?<eng>\w+)$",
        RegexOptions.Compiled);

    private static readonly Regex MemoryInstanceRegex = new(
        @"luid_(?<luid>0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+)",
        RegexOptions.Compiled);

    internal static bool EngineCategoryExists()
    {
        try { return PerformanceCounterCategory.Exists(EngineCategory); }
        catch { return false; }
    }

    internal static bool MemoryCategoryExists()
    {
        try { return PerformanceCounterCategory.Exists(MemoryCategory); }
        catch { return false; }
    }

    internal static string[] GetEngineInstanceNames()
    {
        try { return new PerformanceCounterCategory(EngineCategory).GetInstanceNames(); }
        catch { return []; }
    }

    internal static string[] GetMemoryInstanceNames()
    {
        try { return new PerformanceCounterCategory(MemoryCategory).GetInstanceNames(); }
        catch { return []; }
    }

    internal static bool TryParseEngineInstance(string instanceName, out string luid, out string engineType)
    {
        var m = EngineInstanceRegex.Match(instanceName);
        if (!m.Success)
        {
            luid = string.Empty;
            engineType = string.Empty;
            return false;
        }

        luid = m.Groups["luid"].Value;
        engineType = m.Groups["eng"].Value;
        return true;
    }

    internal static bool TryParseMemoryInstance(string instanceName, out string luid)
    {
        var m = MemoryInstanceRegex.Match(instanceName);
        luid = m.Success ? m.Groups["luid"].Value : string.Empty;
        return m.Success;
    }

    internal static PerformanceCounter CreateEngineCounter(string instanceName) =>
        new(EngineCategory, "Utilization Percentage", instanceName, readOnly: true);

    internal static PerformanceCounter CreateMemoryDedicatedCounter(string instanceName) =>
        new(MemoryCategory, "Dedicated Usage", instanceName, readOnly: true);

    internal static PerformanceCounter CreateMemorySharedCounter(string instanceName) =>
        new(MemoryCategory, "Shared Usage", instanceName, readOnly: true);

    /// <summary>
    /// Enumerates physical (non-virtual) display adapters via WMI, filtering out the
    /// "Microsoft Basic Render/Remote Display" pseudo-adapters. WMI does not expose a
    /// LUID, so correlation with perf-counter LUID groups falls back to enumeration
    /// order — a documented best-effort limitation (see docs/TROUBLESHOOTING.md).
    /// </summary>
    internal static List<WmiAdapterRaw> GetWmiAdapters()
    {
        var results = new List<WmiAdapterRaw>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (var managementObject in searcher.Get())
            {
                using var obj = managementObject;
                var name = obj["Name"] as string ?? "Unknown GPU";
                if (name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Remote Display", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ulong? ram = null;
                if (obj["AdapterRAM"] is uint raw && raw > 0 && raw != uint.MaxValue)
                {
                    ram = raw;
                }

                results.Add(new WmiAdapterRaw(name, ram));
            }
        }
        catch
        {
            // WMI unavailable/restricted in this environment — caller treats this as "no adapters found".
        }

        return results;
    }
}
