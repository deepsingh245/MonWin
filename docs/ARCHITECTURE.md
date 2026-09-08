# Architecture

## Layering

```
Windows (MainWindow / SettingsWindow / AboutWindow, XAML)
    ↓ data binding
ViewModels (MainViewModel / SettingsViewModel / AboutViewModel)
    ↓ interfaces (ICpuMonitor, IMemoryMonitor, IGpuMonitor, ISettingsService, ...)
Services (CpuMonitorService, MemoryMonitorService, GpuMonitorService, SystemMonitorService,
          TaskbarService, StartupService, SettingsService, ThemeService, LoggingService)
    ↓ P/Invoke + WMI + Performance Counters
Native (NativeMethods, MemoryInterop, TaskbarInterop, GpuInterop, MouseHookInterop) + Windows APIs
```

Windows never call a Win32 API directly — they bind to ViewModel properties/commands.
ViewModels never touch `kernel32`/`user32` directly — they depend only on the
`Services` interfaces, which are registered in `App.xaml.cs` via
`Microsoft.Extensions.DependencyInjection`. This keeps every layer above `Native`
unit-testable without real hardware (see `IGpuDataSource` / `FakeGpuDataSource` in the
test project for the pattern used to test GPU aggregation logic without a GPU).

## Polling and history

`SystemMonitorService` owns a single `System.Threading.Timer` (interval from
`AppSettings.UpdateIntervalMs`, default 500ms) that samples CPU/RAM/GPU each tick,
appends to three `RollingHistory` circular buffers (capacity = `HistorySeconds * 1000 /
UpdateIntervalMs`), and raises `SnapshotUpdated`. `MainViewModel` marshals that event to
the UI thread via the injected `Dispatcher` and updates bound properties; `SparklineControl`
repaints only when new data arrives (via `OnRender`, no per-frame animation timer). Each
tick is guarded against overlapping itself (`Interlocked.CompareExchange`) — a sample
that runs long (a slow perf-counter refresh, a GC pause) is skipped rather than allowed
to run concurrently with the next one, since the GPU data source's counter caches aren't
designed to be touched from two threads at once.

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

`SettingsService` reads/writes `AppSettings` as JSON at
`%LOCALAPPDATA%\SystemMonitor\settings.json` (atomic write: temp file + move). Loading
validates every field against its known-valid range and replaces anything out of range
with a safe default (`SettingsService.Sanitize`) rather than trusting the file blindly —
see `docs/SECURITY.md#settings-file-integrity` for why that matters and
`SettingsServiceTests` for the cases covered.
