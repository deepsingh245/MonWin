# Architecture

MonWin is one shared core plus one app per desktop platform. This document describes the
shared structure first, then the Windows (WPF) implementation in detail, then the Linux
(Avalonia) implementation, and finally what a future macOS port needs.

## Projects

```
src/MonWin.Core       net8.0           Shared logic (no UI framework, no OS API)
src/SystemMonitor     net8.0-windows   Windows app: WPF UI + Win32/WMI providers (SystemMonitor.exe)
src/MonWin.Linux      net8.0           Linux app: Avalonia UI + /proc, /sys, NVML providers
tests/MonWin.Core.Tests    net8.0           Tests for the shared core (run on any OS)
tests/SystemMonitor.Tests  net8.0-windows   Windows-only tests (Win32/registry/WPF)
tests/MonWin.Linux.Tests   net8.0           Linux providers and integration (fixtures; run on any OS)
```

**`MonWin.Core`** holds everything that is the same on every OS: the models
(`CpuMetrics`, `MonitorSnapshot`, `AppSettings`...), settings load/save/validation,
`SystemMonitorService` (the poll timer, rolling history and snapshot event),
`RollingHistory`, `CpuUsageCalculator`, `ThresholdEvaluator`, `RateFormatter`,
`OverlayPlacement` (pure overlay-positioning math), `NetworkMonitorService` (it uses
`System.Net.NetworkInformation`, which works on both OSes), `DiskMonitorService`, and the view models
(`MainViewModel`, `SettingsViewModel`, `AboutViewModel`). It also declares the interfaces
each OS must implement. It references no UI framework and no OS API.

**Legacy names, on purpose.** The shared code keeps the `SystemMonitor.*` namespaces, and
the Windows app keeps the `SystemMonitor` assembly/exe name, the `%LOCALAPPDATA%\SystemMonitor`
settings folder and the `MonWinSystemMonitor` startup registry value. Renaming any of them
would orphan existing Windows installs' settings and autostart entries for no user benefit,
so a rename (if ever wanted) is a separate change that needs a migration first.

### Where the OS is chosen

Shared code never asks "am I on Windows or Linux?". Each app has a *composition root*
that registers the OS-specific implementations of the shared interfaces with
`Microsoft.Extensions.DependencyInjection`:

| Interface (in `MonWin.Core`) | Windows (`SystemMonitor`) | Linux (`MonWin.Linux`) |
|---|---|---|
| `ICpuMonitor` | `CpuMonitorService` (`GetSystemTimes`) | `LinuxCpuMonitor` (`/proc/stat`) |
| `IMemoryMonitor` | `MemoryMonitorService` (`GlobalMemoryStatusEx`) | `LinuxMemoryMonitor` (`/proc/meminfo`) |
| `IGpuMonitor` | `GpuMonitorService` (perf counters + WMI) | `LinuxGpuMonitor` (NVML, amdgpu sysfs, DRM) |
| `IDiskDataSource` | `WindowsDiskDataSource` (PhysicalDisk counters) | `LinuxDiskDataSource` (`/proc/diskstats`) |
| `INetworkMonitor` | shared `NetworkMonitorService` | shared `NetworkMonitorService` |
| `IStartupService` | `StartupService` (HKCU Run key) | `LinuxStartupService` (XDG autostart file) |
| `IThemeService` | `ThemeService` (registry) | `AvaloniaThemeService` (desktop preference) |
| `ITaskbarService` | `TaskbarService` (`Shell_TrayWnd`) | `LinuxPanelService` (floats in the work area) |
| `IHotkeyService` | `HotkeyService` (`RegisterHotKey`) | `UnsupportedHotkeyService` |
| `IAppPaths` | `WindowsAppPaths` (`%LOCALAPPDATA%\SystemMonitor`) | `LinuxAppPaths` (XDG directories) |
| `IUiDispatcher` | `WpfUiDispatcher` | `AvaloniaUiDispatcher` |
| `IColorPickerService` | `WinFormsColorPicker` | `NoColorPickerService` (presets + `#RRGGBB` box) |

`App.ConfigureServices` (Windows) and `App.Start` (Linux) call
`AddMonWinCore()` plus their own `AddWindowsPlatform()` / `AddLinuxPlatform()`. A test per
platform builds that provider and resolves every interface, so a missing registration fails
a test rather than a launch.

## UI decision: keep WPF on Windows, Avalonia on Linux

Two approaches were considered: (A) keep the WPF app and add a separate Linux UI over the
shared core, or (B) migrate the UI to a cross-platform framework everywhere.

**Chosen: A.** The Windows overlay is deeply Win32 - it locates the taskbar rectangle,
sets `WS_EX_TOOLWINDOW/NOACTIVATE/TRANSPARENT`, re-asserts `HWND_TOPMOST`, uses a
low-level mouse hook and `RegisterHotKey`. Re-implementing that on another framework would
put the released Windows app at risk for no user-visible gain. Avalonia 11 (MIT) is a
lightweight, custom-rendered UI toolkit that runs on Linux and can later serve macOS.

Trade-offs accepted: two UI projects to maintain (XAML written twice) and a visual design
kept in step by hand. They are mitigated by putting all view models, formatting, thresholds,
positioning math and settings in `MonWin.Core`, so the two UIs are thin views over the same
bindable properties. Avalonia's Linux backend is X11; on Wayland sessions it runs through
XWayland, so MonWin makes **no** promise of native-Wayland positioning or panel embedding.

## Polling and history

`SystemMonitorService` (shared) owns a single `System.Threading.Timer` (interval from
`AppSettings.UpdateIntervalMs`, default 500ms) that samples CPU, RAM, GPU, network and disk
each tick, appends to five `RollingHistory` circular buffers (capacity = `HistorySeconds *
1000 / UpdateIntervalMs`), and raises `SnapshotUpdated`. `MainViewModel` marshals that event
to the UI thread via the injected `IUiDispatcher` and updates bound properties; the sparkline
control repaints only when new data arrives (custom `OnRender`/`Render`, no per-frame
animation timer). Each tick is guarded against overlapping itself
(`Interlocked.CompareExchange`) — a sample that runs long (a slow perf-counter refresh, a GC
pause) is skipped rather than allowed to run concurrently with the next one, since the GPU
data source's counter caches aren't designed to be touched from two threads at once.

## Windows implementation

The rest of this section describes the WPF app.

## One window, two presentations

`MainWindow` is the only window that shows live metrics. It has two visual states,
switched by toggling which internal `StackPanel` is visible and which card `Style` is
applied — never by changing `WindowStyle`/`AllowsTransparency` after the window handle
exists (WPF disallows that):

- **Overlay**: tiny, chromeless, sitting directly on the taskbar (Compact or
  Compact+Graph, per settings).
- **Detailed**: larger, decorated card, centered on screen, opened by left-click, the
  tray's "Open Monitor", or `DisplayMode = Detailed`.

`SettingsWindow` is a non-modal flyout (see "Settings dismissal" below); `AboutWindow`
is a simple modal dialog. Both are owned by `MainWindow` and share its base
`MonWin.Window.Overlay` style (chromeless, `Topmost`), so both need the topmost
coordination described next.

## Staying on top without fighting itself

The overlay is topmost so it survives above normal windows, but the taskbar
(`Shell_TrayWnd`) is *also* topmost, and focusing/clicking it can restack it above
ours within that topmost band — a one-time `SetWindowPos(HWND_TOPMOST)` at show-time
doesn't survive that. `MainWindow` re-asserts topmost on a 1-second `DispatcherTimer`
so it pops back above the taskbar rather than staying stuck behind it.

That re-assertion would just as happily bury `SettingsWindow`/`AboutWindow` (also
topmost, but only asserted once at their own show-time) every time the overlay is
clicked. `MainWindow.SetTopmostReassertionSuspended(bool)` pauses the timer — and the
direct `SetTopmost()` calls inside `ShowOverlay()`/`ShowDetailed()`, which needed the
same guard — for as long as a dialog it owns is open; `App.OpenSettings()`/`OpenAbout()`
call it around showing/closing those windows.

## Settings dismissal

`SettingsWindow` closes when you click anywhere outside its own bounds — desktop,
another app, or the overlay itself — like a Windows 11 flyout, rather than requiring an
explicit button click. This is implemented with a scoped low-level mouse hook
(`Native/MouseHookInterop.cs`), not WPF's `Window.Deactivated`: activation-based
detection missed plain desktop clicks and mis-fired from this app's own overlay
window activating itself (`ShowDetailed()` calls `.Activate()`). The hook installs on
`Loaded`, compares each click's screen position against the window's own rect, and
uninstalls on `Closed` — see `docs/SECURITY.md` for exactly what it does and doesn't
observe. Opening the native color picker suspends it (`SettingsViewModel.
ExternalDialogOpening`/`ExternalDialogClosed`), since that dialog taking focus is
expected, not an "outside click".

## Direct manipulation: drag and resize

The overlay card can be dragged and resized in place, both persisted to settings
immediately (no explicit "Save"):

- **Drag to move** (`MainWindow.OnCardPreviewMouseLeftButtonDown`): calls `DragMove()`,
  then compares `Left`/`Top` before and after to tell a real drag from a plain click —
  `DragMove()` only actually moves the window once the OS's own drag threshold is
  exceeded, so a stationary click leaves position unchanged. A real drag switches
  `AppSettings.Position` to `Custom` and stores the drop point (converted to physical
  pixels via the window's DPI scale). Clicks that land on a real button inside the card
  (e.g. the Detailed view's close button) are explicitly excluded from this check —
  otherwise the tunneling `PreviewMouseLeftButtonDown` would swallow the click before
  it ever reached the button.
- **Drag to resize** (`ResizeThumb`, a WPF `Thumb` positioned at the card's corner):
  drives a `ScaleTransform` set as `CardBorder.LayoutTransform`, so the whole card —
  text, sparklines, everything — scales as one unit and WPF's own layout system
  accounts for the transformed size (unlike a `RenderTransform`, which would just
  stretch pixels without the window resizing to match). The thumb sits *outside* the
  scaled `CardBorder` (a sibling in the outer `Grid`) specifically so its own drag
  deltas stay in untransformed screen units — nesting it inside the scaled element
  would make resize speed vary with the current zoom level. Persisted as
  `AppSettings.OverlayScale`, clamped to `[0.7, 2.0]`.

## Taskbar tracking and positioning

`TaskbarService` locates the taskbar via `Shell_TrayWnd` + `SHAppBarMessage` (see
`docs/SECURITY.md` for the exact APIs) and computes where the overlay should sit. For
the anchored positions (Left/Center/Right — not `Custom`, which ignores the taskbar
entirely), it centers the overlay vertically *within* the taskbar's own band whenever
the card is small enough to fit, so it reads as part of the taskbar rather than a
separate floating bar; if the card has been resized taller than the taskbar itself, it
falls back to floating just above it instead of overlapping both edges.

`MainWindow` hooks its own `HwndSource` for `TaskbarCreated`, `WM_DISPLAYCHANGE`, and
`WM_DPICHANGED` to recompute position when Explorer restarts, a display is added or
removed, or DPI changes — deliberately not `WM_SETTINGCHANGE`, which Windows broadcasts
for many unrelated settings and caused positional jitter with no real trigger behind
it. Taskbar bounds come back in **physical** pixel coordinates from the raw Win32
calls; the work-area fallback uses `System.Windows.Forms.Screen.PrimaryScreen.
WorkingArea` (also physical pixels) rather than WPF's `SystemParameters.WorkArea`
(DPI-virtualized to the primary monitor) so the two never get mixed under non-100%
scaling. If `Shell_TrayWnd` can't be found at all, the overlay floats in the
work area's bottom-right corner instead.

## GPU aggregation

`GpuMonitorService.Aggregate` (in `Services/GpuMonitorService.cs`) is a pure, testable
static method: given raw per-engine utilization samples and a LUID→adapter mapping, it
takes the **max** utilization across engine types per adapter (not a sum — engines run
concurrently, so summing overstates load), and either uses the explicitly selected
adapter or auto-selects the one currently doing the most work. See
`GpuMonitorServiceTests` for the exact scenarios this covers.

## Theming and accent color

`App.ApplyTheme()` merges either `Resources/Themes/Light.xaml` or `Dark.xaml` into
`Application.Resources` based on `ThemeService.Resolve(AppSettings.Theme)` (which reads
the Windows personalization registry value when set to "System"). A custom accent
color, if set, is layered on top: `App.ApplyAccentOverride` writes `SolidColorBrush`
values directly onto `Application.Resources["MonWin.Accent"]`/`"MonWin.AccentFill"` —
keys set directly on a `ResourceDictionary` take precedence over anything pulled in via
`MergedDictionaries`, so this cleanly overrides whichever theme is active without the
two mechanisms needing to know about each other. Clearing the custom color removes the
override and the theme's own default shows through again. Both theme and accent are
re-applied whenever settings are saved, so changes take effect immediately.

## Settings persistence and integrity

`SettingsService` (shared) reads/writes `AppSettings` as JSON in the directory given by
`IAppPaths.ConfigDirectory` - on Windows `%LOCALAPPDATA%\SystemMonitor\settings.json` (atomic
write: temp file + move). Loading
validates every field against its known-valid range and replaces anything out of range
with a safe default (`SettingsService.Sanitize`) rather than trusting the file blindly —
see `docs/SECURITY.md#settings-file-integrity` for why that matters and
`SettingsServiceTests` for the cases covered.

## Linux implementation

`MonWin.Linux` is an Avalonia 11 app over `MonWin.Core`. It is **experimental**: the logic
is covered by automated tests that run against fixture `/proc` and `/sys` files, but it has
not yet been run on a real Linux desktop (see the README's compatibility table).

### Metric providers

All readers go through `LinuxFiles`, a thin wrapper that returns "no data" for any missing or
unreadable file, so a different kernel, a container or missing permissions degrade to "N/A"
instead of an exception. Tests point it at a temp directory of fixture files.

- **CPU** - the aggregate `cpu` line of `/proc/stat`. Jiffies are mapped to the convention the
  shared `CpuUsageCalculator` already uses for Windows (idle = idle+iowait; "kernel" =
  system+irq+softirq+idle+iowait; user = user+nice+steal), so the delta math is not duplicated.
  Frequency is the mean of `cpufreq/scaling_cur_freq` (falling back to `/proc/cpuinfo`).
- **RAM** - `MemTotal` and `MemAvailable` from `/proc/meminfo` (estimated from free+buffers+cache
  on kernels older than 3.14), via `MemoryMetrics.FromTotals`.
- **Disk** - `/proc/diskstats` deltas over whole physical devices only (partitions, loop, ram,
  zram, device-mapper, md and optical drives are excluded because they double-count). Speed is
  summed; "active %" is the *busiest single disk* (`io_ticks`), so a saturated disk is not
  hidden by an idle one.
- **Network** - the shared `NetworkMonitorService`.
- **GPU** - every source is optional and "unsupported" is never reported as 0%:
  NVIDIA through NVML (`libnvidia-ml.so.1`, loaded at runtime, so there is no install-time
  dependency), AMD through `amdgpu` sysfs (`gpu_busy_percent`, `mem_info_vram_*`), and any other
  DRM card by name only. Intel exposes no utilization without the perf PMU or root, which MonWin
  does not use, so Intel shows N/A.

### Desktop integration and its limits

There is no universal Linux taskbar to embed into: GNOME, KDE, Xfce and tiling window managers
each draw their own, and Wayland gives applications no supported way to dock into a panel or
to choose their own global position or stacking order. MonWin therefore does not attempt it.

- **Overlay** - a borderless, transparent, topmost window that floats along the bottom edge of
  the screen's *work area* (which already excludes the desktop's panels), anchored Left / Center
  / Right or at a custom dragged position (`OverlayPlacement.ComputeFloating`). Dragging is
  implemented by moving the window directly so a click and a drag can be told apart on every
  desktop. Whether the window manager honors the requested position or always-on-top is its
  decision; `DesktopSession` only records the session type for the log.
- **Tray** - Avalonia's `TrayIcon` (StatusNotifierItem over D-Bus). KDE has a host; GNOME needs
  the AppIndicator extension. Without a host there is simply no tray icon, and the overlay's
  right-click menu offers the same actions.
- **Autostart** - `~/.config/autostart/monwin.desktop` (XDG Autostart), written atomically and
  removed again when switched off.
- **Paths** - settings in `$XDG_CONFIG_HOME/monwin` (default `~/.config/monwin`), logs in
  `$XDG_STATE_HOME/monwin/logs` (default `~/.local/state/monwin/logs`); relative or empty XDG
  variables are ignored, as the spec requires.
- **Single instance** - an exclusive lock on `$XDG_RUNTIME_DIR/monwin.lock`, released by the OS
  on exit even after a crash.
- **Not available on Linux yet** (hidden in its Settings): global hotkey, click-through, the
  live-CPU tray icon, and the click-outside-to-dismiss Settings flyout (it needs a global mouse
  hook, which Linux desktops don't allow).
- **Theme** - Light/Dark/System through Avalonia's platform settings; colors are the same values
  as the Windows themes, written into the app resources from `ThemePalette`.

## Adding macOS later

Nothing here implements or claims macOS support. A port would add a third app project
(for example `MonWin.macOS`, Avalonia or AppKit) with its own composition root, reusing
`MonWin.Core` unchanged:

- providers for the interfaces above (CPU/RAM through Mach APIs such as `host_statistics`/
  `host_processor_info`, disks and network through IOKit/`getifaddrs`, GPU through IOKit/Metal
  where permitted - Apple Silicon needs its own investigation);
- `IAppPaths` returning `~/Library/Application Support/MonWin` (settings) and
  `~/Library/Logs/MonWin` (logs);
- a menu-bar item instead of a taskbar overlay, a login item (SMAppService) for startup, and
  app-bundle packaging, signing and notarization.

To keep that possible, `MonWin.Core` must stay free of Windows- and Linux-specific types, new
platform behavior goes behind an interface, and packaging stays per-platform.
