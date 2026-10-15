# MonWin — System Monitor for Windows and Linux

A small, lightweight system monitor for CPU, RAM, GPU, network and disk usage that sits
on your screen as a compact overlay. On Windows it docks directly onto the taskbar; on
Linux (experimental) it floats along the bottom of your screen. A safe, user-mode
alternative to tools that rely on kernel drivers (WinRing0, OpenHardwareMonitor/
LibreHardwareMonitor drivers, etc.) — see [Security and privacy](#security-and-privacy).

```
CPU 32%  ▁▂▃▅▃▂    RAM 49%  ▃▄▃▄▅    GPU 1%  ▁▁▁▂▁    NET ↓1.2 MB/s ↑85 KB/s  ▁▃▂▅▂    DISK 4%  ▁▁▃▁▁
```

## Supported platforms

| Feature | Windows 10/11 | Linux | macOS |
|---|---|---|---|
| CPU monitoring | Supported | Experimental | Planned |
| RAM monitoring | Supported | Experimental | Planned |
| GPU monitoring | Supported | Experimental — AMD and NVIDIA report usage; Intel and others show N/A | Planned |
| Network monitoring | Supported | Experimental | Planned |
| Disk monitoring | Supported | Experimental | Planned |
| Overlay on the taskbar | Supported (docked on the taskbar) | Unsupported — no universal taskbar exists; floating overlay instead (Experimental) | Planned (menu bar) |
| System tray icon | Supported | Experimental (needs a StatusNotifier host; GNOME needs an extension) | Planned |
| Start with Windows / session | Supported | Experimental (XDG autostart) | Planned |
| Threshold colors, hover tooltip, per-metric graphs, bits/bytes | Supported | Experimental | Planned |
| Light / dark / accent color | Supported | Experimental | Planned |
| Global show/hide hotkey | Supported | Unsupported | Planned |
| Click-through overlay | Supported | Unsupported | Planned |
| Click-outside-to-close Settings | Supported | Unsupported (normal window) | — |

**Supported** = implemented, tested, and shipped in a release for that platform.
**Experimental** = implemented and covered by automated tests, but not yet verified on a real
Linux desktop — expect rough edges, and please report them. **Planned** = intended, not
started. **Unsupported** = not possible or deliberately not offered on that platform.
macOS is **not** supported yet; it is only prepared for in the architecture
([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#adding-macos-later)).

## Features

- Live CPU (% + frequency), RAM (% + used/total), and GPU (% + memory) with rolling
  sparkline history (30/60/120/300s, configurable).
- Network throughput (download/upload across active adapters, auto-scaling graph, shown in
  bytes or bits per second) and disk activity (active time % plus read/write speed in the
  detailed view). Both are opt-in: Settings → Metrics.
- Threshold colors: a value turns amber, then red, when it crosses limits you set per metric
  (default 75% / 90%).
- Rich hover tooltip with every enabled metric's details, and a per-metric choice of
  graph or text-only in the compact view.
- Compact, Compact+Graph, and Detailed display modes.
- Sits directly on the Windows taskbar (Left/Center/Right, or a custom position you drag it
  to); falls back to a floating window if the taskbar can't be located. On Linux it floats
  along the bottom of the usable screen area and can be dragged anywhere.
- Follows the system light/dark theme, or set it manually; customizable accent color.
- Left-click for the detailed view, right-click for the menu, middle-click to hide/show.
  Drag the overlay to move it (it remembers the spot); drag the corner grip to resize
  (double-click the grip to reset).
- System tray icon; closing the detailed view does not stop monitoring.
- Windows only: global show/hide hotkey (Ctrl+Alt+M by default), optional live CPU % in the
  tray icon, click-through mode, and a Settings flyout that closes when you click outside it.
- Local JSON settings, local rotating log file, zero network access.

## Screenshots

There are no screenshots in the repository yet (none exist for the Linux app at all, since it
has not been run on a real desktop). Contributions are welcome.

## Installation

### Windows installation

Requirements: Windows 10 (1803+) or Windows 11, x64. No administrator rights, no driver, and no
.NET install are needed (the build is self-contained).

1. Download `SystemMonitor.exe` (or `MonWin-<version>-win-x64.zip`) from the Releases page, or
   build it yourself (see [Building from source](#building-from-source)).
2. Run it. It starts monitoring immediately with CPU+RAM+GPU in Compact+Graph mode, sitting
   on the taskbar.
3. Right-click the overlay (or the tray icon) for Settings, including "Start with Windows".

### Linux installation (experimental)

Requirements: 64-bit Linux (x86-64) with a graphical session — X11, or Wayland with
XWayland — and the usual desktop libraries (fontconfig, libX11, libICE, libSM). No .NET install
is needed (the runtime is bundled) and no root access.

A Linux release asset is produced by the build scripts and by CI
(`MonWin-<version>-linux-x64.tar.gz`); it is attached to a GitHub release only when the
maintainer publishes one. Then:

```bash
tar -xzf MonWin-<version>-linux-x64.tar.gz
cd MonWin-<version>-linux-x64
./install.sh            # per user: ~/.local/share/monwin, ~/.local/bin/monwin, menu entry, icon
monwin                  # or start it from your applications menu
./install.sh --uninstall   # removes it again; your settings are kept
```

You can also just run `./monwin` from the extracted folder. Packaging is a portable tar.gz
for now; AppImage/DEB/RPM are future work. To build it yourself, see
[docs/BUILD.md](docs/BUILD.md#linux).

## Usage

- **Left-click** the overlay for the detailed card; the ✕ button or **Show Compact View** returns
  to the overlay. **Right-click** for the menu (Settings, Pause, Start with Windows/session,
  About, Exit). **Middle-click** hides/shows it.
- **Drag** the overlay to move it; **drag the corner grip** to resize it.
- Hover the overlay for a tooltip with the details of every enabled metric.
- The tray icon offers the same actions. On Linux, if your desktop shows no tray icon, use
  the overlay's right-click menu.

## Configuration

Settings (right-click → Settings) are grouped into General, Display, Metrics and Alerts tabs:

- **General**: start with Windows/session, start minimized, update interval (250ms–2s), history
  length (30–300s), tooltip, and — on Windows — click-through, live CPU % in the tray icon and
  the global shortcut.
- **Display**: Compact / Compact+Graph / Detailed; overlay position (Left/Center/Right/Custom);
  theme (System/Light/Dark); accent color (presets, plus the native color picker on Windows
  or a `#RRGGBB` box on Linux).
- **Metrics**: which metrics to show (CPU, RAM, GPU on by default; Network and Disk off), a
  graph on/off switch per metric, and bytes vs. bits for network speed.
- **Alerts**: the warning/critical percentage at which each value changes color.

Beyond the Settings window, the overlay itself is directly manipulable: drag it to
reposition and drag the small corner grip to resize — both persist immediately.

Settings are plain JSON: `%LOCALAPPDATA%\SystemMonitor\settings.json` on Windows,
`~/.config/monwin/settings.json` on Linux. Logs are in `%LOCALAPPDATA%\SystemMonitor\logs`
and `~/.local/state/monwin/logs`. On macOS (when supported) the plan is
`~/Library/Application Support/MonWin` and `~/Library/Logs/MonWin`.

## Platform-specific limitations

**Windows**

- True native taskbar embedding is not possible on Windows 11 through supported APIs; MonWin
  renders its own window sized to fit on the taskbar instead (by design — see
  [Taskbar positioning](#taskbar-positioning)).
- GPU total VRAM can show "Unknown" on some >4GB cards due to a WMI/driver reporting
  limitation, not a MonWin bug (docs/TROUBLESHOOTING.md).
- Multi-GPU adapter-to-name correlation is best-effort (docs/ARCHITECTURE.md).
- Network throughput sums all active adapters, so traffic through a VPN can be counted twice
  (once on the VPN adapter, once on the physical one). Disk activity is the aggregate across
  all physical disks, not per-drive.

**Linux (experimental)**

- Linux has no universal taskbar, and Wayland forbids apps from docking into panels or choosing
  global window positions, so MonWin floats along the bottom of the usable screen area. The
  window manager or compositor may ignore the requested position or always-on-top. MonWin
  does not try to bypass these restrictions.
- The tray icon needs a StatusNotifier host (KDE has one; GNOME needs the "AppIndicator"
  extension); otherwise use the overlay's right-click menu.
- GPU usage: AMD (`amdgpu`) and NVIDIA (proprietary driver, via NVML) are supported; Intel and
  most other drivers expose no unprivileged utilization counter, so they show N/A — never a
  fake 0%.
- No global hotkey, no click-through, no live CPU tray icon, and Settings is a normal window
  (these need global input hooks that Linux desktops don't allow).
- The disk percentage is the busiest single physical disk; speed is the total over physical
  disks.
- x86-64 only for now: no arm64 build is produced or tested.
- The Linux app has been built and unit-tested, but has **not yet been run on a real Linux
  desktop**. See [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#linux).

## Taskbar positioning

(Windows) Windows 11 doesn't expose a supported way for third-party apps to embed controls
directly into the real taskbar, and its taskbar only supports the bottom edge (no
top/left/right, unlike older Windows versions) — see
[docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#windows-11-taskbar-position-leftcenterright)
for what that means for the Left/Center/Right setting. MonWin instead renders its own
small always-on-top window sized to fit directly on the taskbar (falling back to
floating just above it, or in a screen corner if the taskbar can't be found at all),
tracking taskbar geometry, DPI, and Explorer restarts. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#taskbar-tracking-and-positioning) for how.

## Security and privacy

No kernel driver or kernel module. No Explorer/DLL injection. No admin or root rights. No
Defender exclusions. No telemetry, no network access. Every metric is read through documented
operating-system interfaces: on Windows, P/Invoke to `kernel32`/`user32`/`shell32`/`powrprof`
plus WMI and the standard performance-counter subsystem; on Linux, `/proc`, `/sys` and NVIDIA's
user-space NVML library. The Network metric only reads the local adapters' byte counters —
MonWin itself opens no connections. Full breakdown, including every dependency and its license,
in [docs/SECURITY.md](docs/SECURITY.md).

MonWin runs entirely on your computer. It collects no telemetry or analytics, has no
account/login, and makes no network requests of any kind — nothing is ever sent
anywhere. Logs contain only operational messages (startup/shutdown/metric failures), never
personal data. Even the build is kept telemetry-free: the Linux UI toolkit's build-time
telemetry package is excluded (see [docs/BUILD.md](docs/BUILD.md#build-time-telemetry-disabled)).

## Building from source

Windows app (needs Windows and the .NET 8 SDK):

```powershell
.\scripts\build.ps1                          # restore + build (Release)
dotnet test SystemMonitor.sln -c Release     # run all tests
.\scripts\publish.ps1                        # self-contained single-file win-x64 exe
.\scripts\package.ps1                        # zip it for distribution
```

Linux app and shared core (any OS with the .NET 8 SDK; Python 3 for packaging):

```bash
scripts/build-linux.sh                       # build + run the shared-core and Linux tests
scripts/publish-linux.sh                     # self-contained linux-x64 build
python3 scripts/package-linux.py             # dist/MonWin-<version>-linux-x64.tar.gz
```

Full details, including a smaller framework-dependent Windows publish option, in
[docs/BUILD.md](docs/BUILD.md).

## Testing

```powershell
dotnet test SystemMonitor.sln -c Release
```

201 tests across three projects (22 Windows-only, 87 shared-core, 92 Linux; the latter two run
on any OS): CPU/RAM/network/disk math, rolling history, settings load/save/corruption recovery and
validation, thresholds, hotkey parsing, overlay-positioning math, GPU aggregation (Windows) and
Linux GPU discovery, Linux `/proc` and `/sys` parsing, XDG paths, autostart, per-platform service
composition, and a Windows smoke test that loads the real windows. These verify logic and
wiring; they do not replace manual testing on a real desktop — in particular, the Linux
desktop experience still needs it.

## Contributing

Issues and pull requests are welcome — especially bug reports from real Linux desktops
(please include your distro, desktop environment, X11 or Wayland, and GPU).

- Keep `MonWin.Core` free of Windows-, Linux- and UI-framework-specific types; put OS behavior
  behind an interface and register it in that platform's composition root
  ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)).
- Add tests for new logic, and keep Windows-only tests out of the cross-platform projects.
- MonWin makes no network connections and collects no telemetry. Changes that add either, or add
  a dependency, need a clear justification and an update to [docs/SECURITY.md](docs/SECURITY.md).
- Update the relevant docs in the same change, and mark a feature as "Supported" only after it
  has been verified on that platform.

## Development

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the project layout (shared core plus one
app per platform), the platform interfaces (`ICpuMonitor`, `IMemoryMonitor`, `IGpuMonitor`,
`IDiskDataSource`, `INetworkMonitor`, `IStartupService`, `ITaskbarService`, `IAppPaths`, ...), the
polling/history model, and the GPU logic. Providers are behind interfaces specifically so
application logic is unit-testable without real hardware.

## Packaging

Windows ships as a portable, self-contained, single-file `.exe` (`scripts\publish.ps1` +
`scripts\package.ps1`) — no installer required. MSIX was considered and intentionally skipped —
see [docs/BUILD.md](docs/BUILD.md#installation-portable) for why. Linux ships as a portable
`linux-x64` tar.gz with a per-user `install.sh`.

## Roadmap

Ideas under consideration — not committed to, and not in any particular release yet.

**Linux and macOS**
- Verify and harden the Linux app on real GNOME, KDE and tiling-WM desktops, X11 and Wayland.
- AppImage / DEB / RPM packages; an arm64 build once it can be built and tested.
- macOS support: a menu-bar app over the shared core (Apple Silicon included). Not started.

**New metrics**
- CPU/GPU temperature — only if it can be done reliably without a driver or admin/root rights,
  in keeping with the security model.
- Battery: charge %, charging state, and time remaining (laptops).
- Per-core CPU bars in the detailed view.
- Ping/latency to a configurable host. This would be opt-in, since it is the first
  feature that sends network traffic.
- Per-disk and per-adapter breakdown, with a picker like the existing GPU adapter one.
- Free space on the system drive.

**Bigger features**
- Top processes by CPU and RAM in the detailed view, with an "End task" action.
- Alerts — a notification when a metric stays above a threshold, or disk space runs low.
- History export to CSV and longer viewable history in the detailed view.
- Multi-monitor support (pin the overlay to the taskbar on a secondary display).
- Vertical layout for left/right-docked taskbars.
- Opt-in update check against GitHub releases.

**Polish**
- Auto-hide while a fullscreen app or game is in the foreground.
- Lower polling rate while hidden or on battery power.
- winget package alongside the portable zip.

## License

MIT — see [LICENSE](LICENSE).
