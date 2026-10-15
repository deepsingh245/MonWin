using System.Text.RegularExpressions;

namespace MonWin.Linux.Providers;

/// <summary>
/// A GPU found under /sys/class/drm. What can be read depends on the driver:
/// <list type="bullet">
/// <item>amdgpu: utilization (gpu_busy_percent) and VRAM used/total, no privileges needed.</item>
/// <item>Intel (i915/xe): no utilization file exists; real utilization needs the perf PMU or
/// root, which MonWin doesn't use, so usage stays unsupported (null), not 0.</item>
/// <item>anything else (nouveau, virtual GPUs...): name only.</item>
/// </list>
/// </summary>
internal sealed partial class DrmGpuSource(LinuxFiles files, string cardName, int vendorId, string name) : IGpuAdapterSource
{
    public const int VendorAmd = 0x1002;
    public const int VendorNvidia = 0x10de;
    public const int VendorIntel = 0x8086;

    private string Device => $"/sys/class/drm/{cardName}/device";

    public string Id => cardName;

    public string Name => name;

    public int VendorId => vendorId;

    public GpuReading Read()
    {
        if (vendorId != VendorAmd)
        {
            return GpuReading.Unsupported;
        }

        double? usage = files.ReadUInt64($"{Device}/gpu_busy_percent") is { } busy
            ? Math.Clamp((double)busy, 0, 100)
            : null;
        return new GpuReading(usage, files.ReadUInt64($"{Device}/mem_info_vram_used"), files.ReadUInt64($"{Device}/mem_info_vram_total"));
    }

    /// <summary>Lists the GPUs the kernel's DRM subsystem knows about (cardN entries, not connectors like card0-HDMI-A-1).</summary>
    public static IReadOnlyList<DrmGpuSource> Discover(LinuxFiles files)
    {
        var result = new List<DrmGpuSource>();
        foreach (var card in files.ListDirectoryNames("/sys/class/drm"))
        {
            if (!CardPattern().IsMatch(card) || files.ReadHex($"/sys/class/drm/{card}/device/vendor") is not { } vendor)
            {
                continue;
            }

            var deviceId = files.ReadHex($"/sys/class/drm/{card}/device/device");
            var product = files.ReadTrimmed($"/sys/class/drm/{card}/device/product_name");
            result.Add(new DrmGpuSource(files, card, vendor, DisplayName(vendor, deviceId, product)));
        }

        return result;
    }

    internal static string DisplayName(int vendor, int? deviceId, string? productName)
    {
        if (!string.IsNullOrWhiteSpace(productName))
        {
            return productName.Trim();
        }

        // pci.ids (the vendor/device name database) isn't parsed; show the vendor plus the
        // PCI id so the user can still tell several GPUs apart.
        var vendorName = vendor switch
        {
            VendorAmd => "AMD",
            VendorNvidia => "NVIDIA",
            VendorIntel => "Intel",
            _ => "Unknown vendor",
        };
        return deviceId is { } id ? $"{vendorName} GPU [{vendor:x4}:{id:x4}]" : $"{vendorName} GPU";
    }

    [GeneratedRegex(@"^card\d+$")]
    private static partial Regex CardPattern();
}
