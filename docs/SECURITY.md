# Security Model

MonWin is a normal, user-mode Windows desktop application. This document states
exactly what it does and does not do, and which Windows APIs back each metric.

## Hard guarantees

- **No kernel driver.** No `.sys` file, no WinRing0/WinRing0x64, no OpenHardwareMonitorLib,
  no LibreHardwareMonitorLib, no BYOVD technique of any kind.
- **No Explorer injection, no DLL injection, no taskbar/shell patching.** The taskbar
  overlay is a normal top-level window positioned next to the real taskbar — it never
  hooks, subclasses, or injects into `explorer.exe`.
- **No administrator privileges.** `app.manifest` declares `requestedExecutionLevel
  level="asInvoker"`. Every API used below is callable by a standard user.
- **No Defender exclusions, no disabling of Windows security features.** The app never
  asks for or configures these, and nothing in the build or install process touches
  Defender or any other security product.
- **No telemetry, no analytics, no network access.** MonWin makes zero outbound network
  connections. There is no HTTP client, no update checker, no crash reporter that phones
  home. See "Privacy" in the README for the full statement.
- **No registry hacks to bypass security.** The only registry write MonWin ever performs
  is a single optional value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  (Start-with-Windows), created only when that setting is turned on, and removed when
  turned off.

## APIs used, per metric

| Metric | API | Where | Notes |
|---|---|---|---|
| CPU usage % | `GetSystemTimes` | `kernel32.dll` | Delta of idle/kernel/user ticks between polls; documented, non-privileged. |
| CPU frequency | `CallNtPowerInformation` (`ProcessorInformation` level) | `powrprof.dll` | Same source Task Manager uses; documented, non-privileged. |
| Processor name | Registry read | `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` | Read-only; no admin needed for reads on this key. |
| RAM usage/used/available/total | `GlobalMemoryStatusEx` | `kernel32.dll` | Standard documented memory-status API. |
| GPU utilization % | "GPU Engine" performance counter category | Windows performance counter subsystem | Backed by the WDDM scheduler (vendor-agnostic; same source as Task Manager's GPU tab). |
| GPU memory usage | "GPU Process Memory" performance counter category | Windows performance counter subsystem | Summed per-adapter across process instances. |
| GPU adapter identity / VRAM capacity | WMI `Win32_VideoController` | WMI | `AdapterRAM` is a 32-bit field and can under-report VRAM on cards with >4GB — MonWin shows "Total: Unknown" rather than a wrong number in that case (see TROUBLESHOOTING.md). |
| Taskbar location/state | `FindWindow("Shell_TrayWnd")`, `SHAppBarMessage` (`ABM_GETTASKBARPOS`, `ABM_GETSTATE`, `ABM_GETAUTOHIDEBAREX`) | `user32.dll` / `shell32.dll` | Read-only queries against the existing taskbar window — no modification of Explorer. |
| Explorer-restart detection | `RegisterWindowMessage("TaskbarCreated")` | `user32.dll` | Standard documented broadcast message every taskbar-aware app listens for. |
| Overlay window behavior | `SetWindowLong(GWL_EXSTYLE, ...)`, `SetWindowPos(HWND_TOPMOST, ...)` | `user32.dll` | Applied only to MonWin's own window (tool-window style, no-activate, optional click-through, topmost). |
| Start with Windows | `HKCU\...\CurrentVersion\Run` | Registry | Standard per-user autostart mechanism; no admin, no scheduled task, no service. |
| Settings click-outside-to-dismiss | `SetWindowsHookEx(WH_MOUSE_LL, ...)` | `user32.dll` | See "About the mouse hook" below. |
| Custom accent color | `System.Windows.Forms.ColorDialog` | .NET (wraps the standard Win32 `ChooseColor` common dialog) | Same picker used by MS Paint, Word, etc.; no custom color-parsing beyond a `#RRGGBB` hex round-trip. |

None of the above requires a driver, administrator rights, or any undocumented/private
API. GPU utilization deliberately avoids DXGI/D3DKMT COM interop in favor of the
performance-counter surface, which is public, stable, and already used by Task Manager
itself.

## About the mouse hook

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

`%LOCALAPPDATA%\SystemMonitor\settings.json` is plain, human-editable JSON with no
signing/integrity check — by design, since it holds no secrets and this is a local,
single-user config file (the same trust model as any other app's local settings).
Loading it validates every field against its known-valid range (interval/history
presets, overlay scale bounds, enum values) and replaces anything out of range with a
safe default, rather than trusting the file blindly — a hand-edited or corrupted file
degrades gracefully instead of crashing the app or rendering a broken UI (see
`SettingsService.Sanitize` and `SettingsServiceTests` for the exact cases covered).

## If a metric can't be read safely

If a Windows API is unavailable (missing performance counter category, WMI restricted,
no `Shell_TrayWnd`, etc.), MonWin shows that metric as "N/A" and continues monitoring
everything else — it never installs a fallback driver or degrades security to fill a
gap. See `docs/TROUBLESHOOTING.md` for what each unavailable-metric case looks like and
why it can happen.
