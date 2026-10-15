# Troubleshooting

The first part of this file is about Windows; Linux-specific problems are in
[Linux](#linux) at the end.

## GPU shows "N/A"

MonWin reads GPU utilization from the Windows "GPU Engine" performance counter category
and memory from "GPU Process Memory". These are present on Windows 10 1803+ with a
WDDM 2.x+ driver, which covers effectively all modern Intel/AMD/NVIDIA drivers. If
either category is missing (very old driver, certain virtualized/RDP sessions, or a
locked-down environment that disables the performance counter subsystem), MonWin logs
a warning to `%LOCALAPPDATA%\SystemMonitor\logs\app.log` and shows "N/A" rather than
guessing or falling back to a driver.

## GPU total memory shows "Unknown"

GPU adapter identity and VRAM capacity come from WMI's `Win32_VideoController.AdapterRAM`,
which is a 32-bit field. On some cards with more than 4GB of VRAM, drivers report a
wrapped/incorrect value through this field (a long-standing Windows/WMI limitation, not
specific to MonWin). When the reported value looks implausible, MonWin shows "Total:
Unknown" instead of a wrong number. Current GPU memory *usage* is unaffected — that
comes from the "GPU Process Memory" counters, not `AdapterRAM`.

## Multiple GPUs: the wrong one seems selected, or the name looks off

Windows' performance counters identify GPU engines by adapter LUID, but WMI's
`Win32_VideoController` does not expose a LUID at all. MonWin correlates the two by
enumeration order as a best-effort heuristic (documented in `docs/ARCHITECTURE.md`).
On most systems (one integrated + one discrete GPU) this lines up correctly; on
unusual multi-GPU configurations it may not. Use Settings → GPU adapter to pick the
correct one explicitly — the auto-selected default is the adapter currently doing the
most work, which is usually right even when the name shown is ambiguous.

## The overlay isn't on the taskbar / taskbar not found

For Left/Center/Right positions, MonWin sits directly on the taskbar (vertically
centered within it) by locating it via `Shell_TrayWnd`. If that window can't be found
(rare — e.g., mid-Explorer-restart), MonWin logs a warning and falls back to a floating
window in the bottom-right of the primary monitor's work area instead. It re-attempts
taskbar detection automatically on the next `TaskbarCreated` broadcast (Explorer
restart), display change, or DPI change. If you've resized the overlay (drag the corner
grip) larger than the taskbar itself, it floats just above the taskbar instead of
overlapping both edges.

## I dragged the overlay and now Left/Center/Right in Settings seem to do nothing

Dragging switches the position to "Custom" (so it stays exactly where you dropped it)
— open Settings and pick Left, Center, or Right again to go back to taskbar-anchored
positioning.

## Taskbar auto-hide

If the taskbar is set to auto-hide, MonWin still anchors to where the taskbar sits when
visible. Windows does not cleanly expose "the auto-hidden taskbar's current on-screen
position" the same way it does for a normal taskbar, so the overlay's position in this
mode is best-effort and may need a manual nudge via the position offset in Settings.

## Multi-monitor / DPI changes

MonWin declares Per-Monitor-V2 DPI awareness (`app.manifest`) and recomputes its
position on `WM_DPICHANGED` and `WM_DISPLAYCHANGE`. Taskbar bounds and the work-area
fallback are both read in physical pixel coordinates (`GetWindowRect`/`SHAppBarMessage`
and `Screen.PrimaryScreen.WorkingArea`, respectively) specifically so they don't drift
apart under non-100% scaling.

## Windows 11 taskbar position (Left/Center/Right)

Windows 11 only supports a bottom taskbar — there is no supported way (without
Explorer-hacking, which this project deliberately avoids) to move the real Windows
taskbar itself to another edge. "Left/Center/Right" in MonWin's settings position the
*overlay* along the bottom taskbar's length, not the taskbar itself.

## The app won't start a second time

MonWin is single-instance (a named mutex). If you believe no instance is running but a
new launch does nothing, check Task Manager for a lingering `SystemMonitor.exe` process
and the tray icon (it may already be running minimized).

## Where are the logs and settings?

- Logs: `%LOCALAPPDATA%\SystemMonitor\logs\app.log` (rotates at ~1MB)
- Settings: `%LOCALAPPDATA%\SystemMonitor\settings.json`

(On Linux they are in `~/.local/state/monwin/logs/app.log` and `~/.config/monwin/settings.json`.)

Deleting `settings.json` resets everything to defaults. MonWin also does this
automatically if it finds the file unreadable, and validates individual fields on every
load — a hand-edited or otherwise out-of-range value (an invalid update interval, an
overlay scale outside 0.7–2.0, etc.) is replaced with its default rather than breaking
the app, so you don't need to delete the whole file just to fix one bad value.

## Linux

MonWin on Linux is experimental. These are the situations that come from how Linux desktops
work, not from bugs.

### The overlay isn't "in" my panel / taskbar

Linux has no single taskbar. GNOME, KDE Plasma, Xfce and tiling window managers each draw their
own panel, and Wayland gives applications no supported way to dock into one or to place their own
windows globally. MonWin therefore floats along the bottom of your screen's *usable* area (the
area that excludes your panels) and lets you drag it anywhere. It will not embed into a panel.

### The overlay ignores its position, or doesn't stay on top

MonWin runs through X11 (on a Wayland session, through XWayland) and only *requests* a position
and always-on-top; the window manager or compositor decides. Some Wayland compositors ignore
both for security reasons, and MonWin does not try to get around that. Workarounds: log in to an
X11 session, or use your desktop's own window rules ("always on top", "keep above") for the
`MonWin` window.

### There is no tray icon

The tray icon uses the StatusNotifierItem protocol. KDE Plasma supports it out of the box. GNOME
does not show tray icons unless you install the "AppIndicator and KStatusNotifierItem Support"
extension. Without a tray host MonWin still works: right-click the overlay for the same menu
(Open Detailed View, Settings, Pause, Exit...).

### GPU shows "N/A" (or the usage is missing)

- **AMD (`amdgpu` driver):** usage and VRAM are read from sysfs and should work. If they show
  N/A, check that `/sys/class/drm/card0/device/gpu_busy_percent` exists and is readable.
- **NVIDIA:** needs NVIDIA's proprietary driver, which provides `libnvidia-ml.so.1`. The open
  `nouveau` driver does not provide it, so MonWin shows the GPU's name with N/A usage. In a
  container, NVML is only present if the NVIDIA container runtime exposes it.
- **Intel (integrated or Arc):** Linux exposes no utilization counter that an unprivileged
  program may read (it needs the perf PMU or root, which MonWin never uses), so Intel GPUs show
  N/A. This is deliberate: MonWin shows N/A rather than a fake 0%.
- Anything else (virtual GPUs, some ARM boards): name only.

MonWin's log (`~/.local/state/monwin/logs/app.log`) records the session type and any read
failure.

### CPU frequency is missing

The frequency comes from `cpufreq`. Virtual machines and some ARM boards don't expose it; MonWin
then falls back to `/proc/cpuinfo` and, if that has no clock either, just omits the GHz figure.

### The Disk value looks different from another tool

MonWin's disk "active %" is the busiest *single* physical disk (from `io_ticks`, as `iostat
%util` does), and speed is the total over all physical disks. Partitions, loop devices, RAID/LVM
layers and optical drives are excluded so the same I/O isn't counted twice.

### Start with my session doesn't work

Turning it on writes `~/.config/autostart/monwin.desktop`. Check that the file exists and that
its `Exec=` path still points at your `monwin` binary (re-toggle the setting after moving or
reinstalling it). Desktop environments run autostart entries only for a normal graphical login.

### Hotkey, click-through and the CPU tray icon are missing

They are Windows-only for now. A global hotkey needs a key grab that Wayland forbids, and
click-through and the click-outside-to-close Settings flyout need global input hooks that Linux
desktops don't allow ordinary applications to install.

### The app won't start a second time

MonWin is single-instance: it holds an exclusive lock on `$XDG_RUNTIME_DIR/monwin.lock`. A second
launch exits quietly because the first copy (possibly hidden in the tray, or started minimized)
is still running. The lock disappears when that process exits, even after a crash.

### The window appears blurry, huge or tiny on a HiDPI screen

Avalonia scales by the screen's reported factor. Scale the overlay with the corner grip
(double-click it to reset); fractional scaling under XWayland can look soft, which is a
compositor limitation.
