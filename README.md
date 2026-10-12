# MonWin — Windows 11 System Monitor

A small, native, Windows-11-styled system monitor for CPU, RAM, GPU, network, and disk
usage that sits directly on your taskbar. A safe, user-mode alternative to tools that rely on kernel
drivers (WinRing0, OpenHardwareMonitor/LibreHardwareMonitor drivers, etc.) — see
[Security model](#security-model) below.

```
CPU 32%  ▁▂▃▅▃▂    RAM 49%  ▃▄▃▄▅    GPU 1%  ▁▁▁▂▁    NET ↓1.2 MB/s ↑85 KB/s  ▁▃▂▅▂    DISK 4%  ▁▁▃▁▁
```

## Features

- Live CPU (% + frequency), RAM (% + used/total), and GPU (% + memory) with rolling
  sparkline history (30/60/120/300s, configurable).
- **New:** Network throughput (download/upload speed across all active adapters,
  auto-scaling graph) and Disk activity (active time % like Task Manager, plus
  read/write speed in the detailed view). Both are opt-in — enable them in
  Settings → Display → Metrics.
- **New:** Launching with Windows now shows the compact overlay straight away (it
  previously started hidden in the tray). Use "Start minimized" if you prefer the old
  behavior.
- Compact, Compact+Graph, and Detailed display modes.
- Sits directly on the taskbar (Left/Center/Right, or a custom position you drag it
  to); falls back to a floating window if the taskbar can't be located.
- Follows Windows light/dark theme, or set it manually; customizable accent color via
  quick presets or the native Windows color picker.
- Left-click for the detailed view, right-click for settings, middle-click to
  hide/show, optional click-through mode.
- Drag the overlay to move it anywhere (remembers the drop spot); drag the small
  corner grip to resize the whole card (double-click the grip to reset).
- Settings opens as a light-dismiss flyout — click anywhere outside it to close, no
  button required.
- System tray icon; closing the detailed view does not stop monitoring.
- Local JSON settings, local rotating log file, zero network access.

## Requirements

- Windows 10 (1803+) or Windows 11, x64.
- No administrator rights needed.
- No driver install, no .NET runtime install needed if you use the self-contained
  publish (see below).

## Install / Run

Portable — no installer:

1. Download or build `publish\SystemMonitor.exe` (see BUILD.md).
2. Run it. It starts monitoring immediately with CPU+RAM+GPU in Compact+Graph mode,
   sitting on the taskbar.
3. Right-click the overlay (or the tray icon) for Settings, including "Start with
   Windows".

## Build

```powershell
.\scripts\build.ps1      # restore + build (Release)
dotnet test SystemMonitor.sln -c Release   # run the test suite
.\scripts\publish.ps1    # self-contained single-file win-x64 exe
.\scripts\package.ps1    # zip it for distribution
```

Full details, including a smaller framework-dependent publish option, in
[docs/BUILD.md](docs/BUILD.md).

## Configuration

Settings (right-click → Settings) cover:

- **General**: start with Windows, start minimized, update interval (250ms–2s),
  history length (30–300s).
- **Display**: Compact / Compact+Graph / Detailed; which metrics to show (CPU, RAM,
  GPU on by default; Network and Disk off by default); overlay
  position (Left/Center/Right/Custom with X/Y offset); theme (System/Light/Dark).
- **Color**: accent color presets, or "Custom..." for the native Windows color picker.
- **Interaction**: tooltip on/off, click-through on/off.

Beyond the Settings window, the overlay itself is directly manipulable: drag it to
reposition (click-through mode disables this, since the window doesn't receive clicks
at all then), and drag the small corner grip to resize — both persist immediately.

Settings persist as JSON at `%LOCALAPPDATA%\SystemMonitor\settings.json`.

## Taskbar positioning

Windows 11 doesn't expose a supported way for third-party apps to embed controls
directly into the real taskbar, and its taskbar only supports the bottom edge (no
top/left/right, unlike older Windows versions) — see
[docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#windows-11-taskbar-position-leftcenterright)
for what that means for the Left/Center/Right setting. MonWin instead renders its own
small always-on-top window sized to fit directly on the taskbar (falling back to
floating just above it, or in a screen corner if the taskbar can't be found at all),
tracking taskbar geometry, DPI, and Explorer restarts. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#taskbar-tracking-and-positioning) for how.

## Security model

No kernel driver. No Explorer/DLL injection. No admin rights. No Defender exclusions.
No telemetry, no network access. Every metric is read through documented Windows APIs
(P/Invoke to `kernel32`/`user32`/`shell32`/`powrprof`, plus WMI and the standard
Windows performance-counter subsystem). The Network metric only reads the local
adapters' byte counters (`System.Net.NetworkInformation`) — MonWin itself still opens
no connections. Full API-by-API breakdown in
[docs/SECURITY.md](docs/SECURITY.md).

## Privacy

MonWin runs entirely on your PC. It collects no telemetry or analytics, has no
account/login, and makes no network requests of any kind — nothing is ever sent
anywhere. Logs (`%LOCALAPPDATA%\SystemMonitor\logs\app.log`) contain only operational
messages (startup/shutdown/metric failures), never personal data.

## Development

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the UI → ViewModel → Service →
Native layering, the polling/history model, and the GPU-aggregation logic. Metric
providers are behind interfaces (`ICpuMonitor`, `IMemoryMonitor`, `IGpuMonitor`,
`IGpuDataSource`, `INetworkMonitor`, `IDiskMonitor`, `IDiskDataSource`) specifically so application logic is unit-testable without real
hardware.

## Testing

```powershell
dotnet test SystemMonitor.sln -c Release
```

62 tests: CPU delta-based usage math, memory percentage/used/available math, network
rate math (including adapter-disconnect clamping), disk active-time/throughput
projection, byte-rate formatting, rolling
history buffer behavior (capacity, drop-oldest, resize), settings load/save/corruption
recovery and field-level validation, taskbar-overlay positioning math (on-taskbar
centering, floating fallback, Custom positioning), GPU aggregation (max-not-sum across
engines, auto-select vs. explicit adapter, unavailable states), and startup-registry
enable/disable round-tripping.

## Packaging

MonWin ships as a portable, self-contained, single-file `.exe`
(`scripts\publish.ps1` + `scripts\package.ps1`) — no installer required. MSIX was
considered and intentionally skipped — see
[docs/BUILD.md](docs/BUILD.md#installation-portable) for why.

## Known limitations

- True native taskbar embedding is not possible on Windows 11 through supported APIs;
  MonWin renders its own window sized to fit on the taskbar instead (by design — see
  Security model).
- GPU total VRAM can show "Unknown" on some >4GB cards due to a WMI/driver reporting
  limitation, not a MonWin bug (docs/TROUBLESHOOTING.md).
- Multi-GPU adapter-to-name correlation is best-effort (docs/ARCHITECTURE.md).
- Network throughput sums all active adapters, so traffic through a VPN can be counted
  twice (once on the VPN adapter, once on the physical one).
- Disk activity is the aggregate across all physical disks, not per-drive.

## Upcoming features

Ideas under consideration — not committed to, and not in any particular release yet.

**Quick wins**
- Threshold colors — values turn amber/red above configurable limits (e.g. CPU > 85%).
- Richer hover tooltip on the compact bar (GHz, RAM GB, GPU name, disk R/W) without
  opening the detailed view.
- Per-metric graph toggle (e.g. show Network as text only to save width).
- Network speed in bits (Mbps) as an alternative to bytes (MB/s).
- Live CPU % drawn into the tray icon, for when the overlay is hidden.
- Global hotkey to show/hide the overlay or open the detailed view.

**New metrics**
- CPU/GPU temperature — only if it can be done reliably without a driver or admin
  rights, in keeping with the [Security model](#security-model).
- Battery: charge %, charging state, and time remaining (laptops).
- Per-core CPU bars in the detailed view.
- Ping/latency to a configurable host. This would be opt-in, since it is the first
  feature that sends network traffic.
- Per-disk and per-adapter breakdown, with a picker like the existing GPU adapter one.
- Free space on the system drive.

**Bigger features**
- Top processes by CPU and RAM in the detailed view, with an "End task" action.
- Alerts — a Windows notification when a metric stays above a threshold, or disk
  space runs low.
- History export to CSV and longer viewable history in the detailed view.
- Multi-monitor support (pin the overlay to the taskbar on a secondary display).
- Vertical layout for left/right-docked taskbars.
- Opt-in update check against GitHub releases.

**Polish**
- Auto-hide while a fullscreen app or game is in the foreground.
- Lower polling rate while hidden or on battery power.
- winget package / installer alongside the portable zip.
- Culture-invariant number formatting.

## License

MIT — see [LICENSE](LICENSE).
