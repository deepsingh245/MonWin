# MonWin — Windows 11 System Monitor

A small, native, Windows-11-styled system monitor for CPU, RAM, and GPU usage, anchored
near the taskbar. Built to be a safe, user-mode alternative to tools that rely on
kernel drivers (WinRing0, OpenHardwareMonitor/LibreHardwareMonitor drivers, etc.).

```
CPU 32%  ▁▂▃▅▃▂    RAM 49%  ▃▄▃▄▅    GPU 1%  ▁▁▁▂▁
```

## Features

- Live CPU (% + frequency), RAM (% + used/total), and GPU (% + memory) with rolling
  sparkline history (30/60/120/300s, configurable).
- Compact, Compact+Graph, and Detailed display modes.
- Taskbar-adjacent overlay (Left/Center/Right, or a custom position) or a floating
  window if the taskbar can't be located.
- Follows Windows light/dark theme, or set it manually.
- Left-click for the detailed view, right-click for settings, middle-click to
  hide/show, optional click-through mode.
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
   positioned near the taskbar.
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
- **Display**: Compact / Compact+Graph / Detailed; which metrics to show; overlay
  position (Left/Center/Right/Custom with X/Y offset); theme (System/Light/Dark).
- **Interaction**: tooltip on/off, click-through on/off.

Settings persist as JSON at `%LOCALAPPDATA%\SystemMonitor\settings.json`.

## Taskbar positioning

Windows 11 doesn't expose a supported way for third-party apps to embed controls
directly into the real taskbar, and its taskbar only supports the bottom edge (no
top/left/right, unlike older Windows versions) — see
[docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#windows-11-taskbar-position-leftcenterright)
for what that means for the Left/Center/Right setting. MonWin instead renders its own
small always-on-top window anchored just above the taskbar, tracking taskbar
geometry, DPI, and Explorer restarts. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#taskbar-tracking) for how.

## Security model

No kernel driver. No Explorer/DLL injection. No admin rights. No Defender exclusions.
No telemetry, no network access. Every metric is read through documented Windows APIs
(P/Invoke to `kernel32`/`user32`/`shell32`/`powrprof`, plus WMI and the standard
Windows performance-counter subsystem). Full API-by-API breakdown in
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
`IGpuDataSource`) specifically so application logic is unit-testable without real
hardware.

## Testing

```powershell
dotnet test SystemMonitor.sln -c Release
```

31 tests: CPU delta-based usage math, memory percentage/used/available math, rolling
history buffer behavior (capacity, drop-oldest, resize), settings load/save/corruption
recovery, GPU aggregation (max-not-sum across engines, auto-select vs. explicit
adapter, unavailable states), and startup-registry enable/disable round-tripping.

## Packaging

Current v1 distribution is a portable, self-contained, single-file `.exe`
(`scripts\publish.ps1` + `scripts\package.ps1`). MSIX was considered and intentionally
deferred — see [docs/BUILD.md](docs/BUILD.md#installation-current-v1-approach-portable)
for why.

## Known limitations

- True native taskbar embedding is not possible on Windows 11 through supported APIs;
  MonWin uses a taskbar-adjacent overlay instead (by design — see Security model).
- GPU total VRAM can show "Unknown" on some >4GB cards due to a WMI/driver reporting
  limitation, not a MonWin bug (docs/TROUBLESHOOTING.md).
- Multi-GPU adapter-to-name correlation is best-effort (docs/ARCHITECTURE.md).
- Network/disk metrics are modeled (`Models/NetworkMetrics.cs`) but not implemented in
  v1 — reserved, disabled in Settings.

## License

MIT — see [LICENSE](LICENSE).
