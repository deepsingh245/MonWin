# Troubleshooting

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

Deleting `settings.json` resets everything to defaults. MonWin also does this
automatically if it finds the file unreadable, and validates individual fields on every
load — a hand-edited or otherwise out-of-range value (an invalid update interval, an
overlay scale outside 0.7–2.0, etc.) is replaced with its default rather than breaking
the app, so you don't need to delete the whole file just to fix one bad value.
