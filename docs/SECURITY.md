# Security Model

MonWin is a normal, user-mode desktop application for Windows and (experimentally) Linux.
This document states exactly what it does and does not do, and which operating-system
interfaces back each metric. The guarantees below hold on both platforms; the per-metric
tables cover [Windows](#apis-used-per-metric-windows) and [Linux](#interfaces-used-per-metric-linux)
separately.

## Hard guarantees

- **No kernel driver.** No `.sys` file, no WinRing0/WinRing0x64, no OpenHardwareMonitorLib,
  no LibreHardwareMonitorLib, no BYOVD technique of any kind. On Linux: no kernel module and
  no use of `/dev/mem`, `msr`, `perf_event_open` or other privileged interfaces.
- **No Explorer injection, no DLL injection, no taskbar/shell patching.** The taskbar
  overlay is a normal top-level window positioned next to the real taskbar — it never
  hooks, subclasses, or injects into `explorer.exe`.
- **No administrator privileges.** `app.manifest` declares `requestedExecutionLevel
  level="asInvoker"`. Every API used below is callable by a standard user. On Linux MonWin
  runs as the logged-in user, never needs root, and `install.sh` installs into the user's
  home directory without `sudo`.
- **No Defender exclusions, no disabling of Windows security features.** The app never
  asks for or configures these, and nothing in the build or install process touches
  Defender or any other security product.
- **No telemetry, no analytics, no network access.** MonWin makes zero outbound network
  connections. There is no HTTP client, no update checker, no crash reporter that phones
  home. See "Privacy" in the README for the full statement. (The Network metric only reads the
  local adapters' byte counters; it never opens a connection. Build-time telemetry from a
  third-party dependency is also disabled - see [Dependencies](#dependencies).)
- **No registry hacks to bypass security.** The only registry write MonWin ever performs
  is a single optional value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  (Start-with-Windows), created only when that setting is turned on, and removed when
  turned off.

## APIs used, per metric (Windows)

| Metric | API | Where | Notes |
|---|---|---|---|
| CPU usage % | `GetSystemTimes` | `kernel32.dll` | Delta of idle/kernel/user ticks between polls; documented, non-privileged. |
| CPU frequency | `CallNtPowerInformation` (`ProcessorInformation` level) | `powrprof.dll` | Same source Task Manager uses; documented, non-privileged. |
| Processor name | Registry read | `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` | Read-only; no admin needed for reads on this key. |
| RAM usage/used/available/total | `GlobalMemoryStatusEx` | `kernel32.dll` | Standard documented memory-status API. |
| GPU utilization % | "GPU Engine" performance counter category | Windows performance counter subsystem | Backed by the WDDM scheduler (vendor-agnostic; same source as Task Manager's GPU tab). |
| GPU memory usage | "GPU Process Memory" performance counter category | Windows performance counter subsystem | Summed per-adapter across process instances. |
| GPU adapter identity / VRAM capacity | WMI `Win32_VideoController` | WMI | `AdapterRAM` is a 32-bit field and can under-report VRAM on cards with >4GB — MonWin shows "Total: Unknown" rather than a wrong number in that case (see TROUBLESHOOTING.md). |
| Network throughput | `NetworkInterface.GetIPStatistics()` (`System.Net.NetworkInformation`) | .NET | Reads each active adapter's cumulative byte counters and diffs them; no sockets, no packet capture. |
| Disk activity | "PhysicalDisk" performance counters (`% Idle Time`, `Disk Read/Write Bytes/sec`, `_Total`) | Windows performance counter subsystem | Same source as Task Manager's disk graph. |
| Taskbar location/state | `FindWindow("Shell_TrayWnd")`, `SHAppBarMessage` (`ABM_GETTASKBARPOS`, `ABM_GETSTATE`, `ABM_GETAUTOHIDEBAREX`) | `user32.dll` / `shell32.dll` | Read-only queries against the existing taskbar window — no modification of Explorer. |
| Explorer-restart detection | `RegisterWindowMessage("TaskbarCreated")` | `user32.dll` | Standard documented broadcast message every taskbar-aware app listens for. |
| Overlay window behavior | `SetWindowLong(GWL_EXSTYLE, ...)`, `SetWindowPos(HWND_TOPMOST, ...)` | `user32.dll` | Applied only to MonWin's own window (tool-window style, no-activate, optional click-through, topmost). |
| Start with Windows | `HKCU\...\CurrentVersion\Run` | Registry | Standard per-user autostart mechanism; no admin, no scheduled task, no service. |
| Global show/hide shortcut | `RegisterHotKey` / `UnregisterHotKey` | `user32.dll` | Registers one optional key combination (default Ctrl+Alt+M) for MonWin's own window; it does not observe other keystrokes. Fails harmlessly if another app owns the combination. |
| Live-CPU tray icon | `Bitmap.GetHicon`, `DestroyIcon` | GDI+ / `user32.dll` | Draws a number into MonWin's own tray icon; the previous icon handle is destroyed each update. |
| Settings click-outside-to-dismiss | `SetWindowsHookEx(WH_MOUSE_LL, ...)` | `user32.dll` | See "About the mouse hook" below. |
| Custom accent color | `System.Windows.Forms.ColorDialog` | .NET (wraps the standard Win32 `ChooseColor` common dialog) | Same picker used by MS Paint, Word, etc.; no custom color-parsing beyond a `#RRGGBB` hex round-trip. |

None of the above requires a driver, administrator rights, or any undocumented/private
API. GPU utilization deliberately avoids DXGI/D3DKMT COM interop in favor of the
performance-counter surface, which is public, stable, and already used by Task Manager
itself.

## Interfaces used, per metric (Linux)

The Linux app is experimental (see the README). It reads only files and libraries available to
an ordinary user, and writes only its own settings, log, lock and autostart files.

| Metric / feature | Interface | Notes |
|---|---|---|
| CPU usage % | `/proc/stat` (aggregate `cpu` line) | Delta of cumulative tick counters between polls. World-readable. |
| CPU frequency, name | `/sys/devices/system/cpu/cpu*/cpufreq/scaling_cur_freq`, `/proc/cpuinfo` | Read-only; frequency is omitted when cpufreq isn't exposed (common in VMs). |
| RAM | `/proc/meminfo` (`MemTotal`, `MemAvailable`) | World-readable. |
| Disk activity / speed | `/proc/diskstats`, `/sys/block` | Whole physical devices only; read-only. |
| Network throughput | `NetworkInterface.GetIPStatistics()` (reads kernel counters) | Same shared code as Windows: counters only, no sockets. |
| GPU (AMD) | `/sys/class/drm/card*/device/{gpu_busy_percent,mem_info_vram_*,vendor,device}` | Read-only sysfs files of the `amdgpu` driver. |
| GPU (NVIDIA) | `libnvidia-ml.so.1` (NVML) via `dlopen` at run time | NVIDIA's own user-space library, the one `nvidia-smi` uses; needs no root. If the library is absent, NVIDIA readings are simply unavailable. MonWin does not spawn `nvidia-smi` or any other process. |
| GPU (Intel and others) | `/sys/class/drm/card*/device/{vendor,device}` for the name only | Utilization needs the perf PMU or root, which MonWin does not use, so these show N/A. |
| Start with session | `~/.config/autostart/monwin.desktop` (XDG Autostart) | A plain per-user file, created only when the setting is on and deleted when it is turned off. |
| Tray icon | D-Bus StatusNotifierItem (through Avalonia) | Talks to the user's own session bus to show a tray icon; no network. |
| Single instance | Exclusive lock on `$XDG_RUNTIME_DIR/monwin.lock` | Released by the OS on exit. |
| Settings and logs | `$XDG_CONFIG_HOME/monwin/settings.json`, `$XDG_STATE_HOME/monwin/logs/app.log` | Plain local files; validated on load exactly as on Windows. |

Linux specifics worth stating:

- There is **no mouse or keyboard hook** on Linux. The Windows Settings flyout's click-outside
  behavior and the global hotkey are not offered there (they would need exactly such hooks or
  key grabs, which Wayland forbids and which MonWin would not use anyway).
- MonWin does not try to bypass compositor restrictions: it does not inject into the shell,
  patch a panel or use unsupported window-manager hacks. It creates its own ordinary window.
- Missing permissions, drivers or hardware never crash the app: the affected metric shows N/A
  while everything else keeps working, and "not supported" is never displayed as 0%.

## Dependencies

MonWin deliberately keeps its dependency set small. Everything below is MIT-licensed (checked
from each package's own metadata) and runs only inside MonWin's process; nothing here makes
network requests at run time.

| Package | Used by | Purpose |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection` / `.Abstractions` | all apps / core | Dependency injection (Microsoft). |
| `System.Management`, `System.Diagnostics.PerformanceCounter` | Windows app | WMI adapter names, performance counters (Microsoft). |
| `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` (11.3.x) | Linux app | UI toolkit; brings `Avalonia.X11`, `Avalonia.FreeDesktop`, `Avalonia.Skia`, `Tmds.DBus.Protocol`, `MicroCom.Runtime`. |
| `SkiaSharp`, `HarfBuzzSharp` (+ native assets) | Linux app (via Avalonia) | 2D rendering and text shaping. |
| xUnit, Microsoft.NET.Test.Sdk, coverlet | tests only | Test framework. |

**Build-time telemetry.** Avalonia's `Avalonia.BuildServices` package reports anonymous
build-time data (machine id, hashed machine and project name, OS version) to Avalonia's servers
whenever an Avalonia project is compiled. It is a development-only dependency and is not part
of the shipped app, but it conflicts with this project's no-telemetry rule, so it is switched
off: the Linux projects reference it with `ExcludeAssets="all"` (its MSBuild task is then never
imported - verified by inspecting the generated `.nuget.g.targets` and a diagnostic build), and
the build scripts and CI also set `AVALONIA_TELEMETRY_OPTOUT=1`. Builds made before this
exclusion was added may have sent that data once per build from the building machine.

The self-contained Linux build also bundles the .NET 8 runtime and the native libraries from
`SkiaSharp.NativeAssets.Linux` 2.88.9 and `HarfBuzzSharp.NativeAssets.Linux` 8.3.1.1 (both MIT).

## About the mouse hook (Windows)

The Settings window closes when you click anywhere outside it (like a Windows 11
flyout). That's implemented with a low-level mouse hook
(`Native/MouseHookInterop.cs` + `Windows/SettingsWindow.xaml.cs`), which is worth
being explicit about since "global mouse hook" can sound alarming out of context:

- It's `WH_MOUSE_LL`, not `WH_KEYBOARD_LL` — it only ever sees mouse button-down
  events and their screen coordinates, never keystrokes, never window contents,
  never clipboard data.
- It's a **low-level** hook, which runs entirely in this process's own thread — unlike
  the older `WH_MOUSE` hook type, it does not require (and this code does not do)
  injecting a DLL into any other process.
- On every event it does exactly one thing: compare the click's (x, y) against the
  Settings window's own rectangle, and close the window if the click fell outside it.
  Nothing is logged, stored, or transmitted — you can verify this by reading
  `HookCallback` in `SettingsWindow.xaml.cs`, which is the entire implementation.
- It always calls `CallNextHookEx` before returning, so it never blocks, delays, or
  alters mouse input for any other application.
- It is installed only while the Settings window is open and removed the instant it
  closes (`Loaded`/`Closed` handlers) — it does not run for the rest of the app's
  lifetime.

## Settings file integrity

`%LOCALAPPDATA%\SystemMonitor\settings.json` (Windows) or `~/.config/monwin/settings.json`
(Linux) is plain, human-editable JSON with no
signing/integrity check — by design, since it holds no secrets and this is a local,
single-user config file (the same trust model as any other app's local settings).
Loading it validates every field against its known-valid range (interval/history
presets, overlay scale bounds, enum values) and replaces anything out of range with a
safe default, rather than trusting the file blindly — a hand-edited or corrupted file
degrades gracefully instead of crashing the app or rendering a broken UI (see
`SettingsService.Sanitize` and `SettingsServiceTests` for the exact cases covered).

## If a metric can't be read safely

If an operating-system interface is unavailable (Windows: missing performance counter category,
WMI restricted, no `Shell_TrayWnd`; Linux: missing `/sys` file, no NVML library, unreadable
`/proc` entry), MonWin shows that metric as "N/A" and continues monitoring
everything else — it never installs a fallback driver or degrades security to fill a
gap. See `docs/TROUBLESHOOTING.md` for what each unavailable-metric case looks like and
why it can happen.
