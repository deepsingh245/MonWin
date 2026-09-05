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
Native (NativeMethods, MemoryInterop, TaskbarInterop, GpuInterop) + Windows APIs
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
repaints only when new data arrives (via `OnRender`, no per-frame animation timer).

## One window, two presentations

`MainWindow` is the only window that shows live metrics. It has two visual states,
switched by toggling which internal `StackPanel` is visible and which card `Style` is
applied — never by changing `WindowStyle`/`AllowsTransparency` after the window handle
exists (WPF disallows that):

- **Overlay**: tiny, chromeless, anchored near the taskbar via `TaskbarService`
  (Compact or Compact+Graph, per settings).
- **Detailed**: larger, decorated card, centered on screen, opened by left-click, the
  tray's "Open Monitor", or `DisplayMode = Detailed`.

`SettingsWindow` and `AboutWindow` are simple modal dialogs, each bound to their own
ViewModel.

## Taskbar tracking

`TaskbarService` wraps `Shell_TrayWnd` + `SHAppBarMessage` lookups (see SECURITY.md for
the exact APIs). `MainWindow` hooks its own `HwndSource` for `TaskbarCreated`,
`WM_DISPLAYCHANGE`, and `WM_DPICHANGED` to recompute position when Explorer restarts,
a display is added/removed, or DPI changes. Taskbar bounds come back in **physical**
pixel coordinates from the raw Win32 calls; the work-area fallback uses
`System.Windows.Forms.Screen.PrimaryScreen.WorkingArea` (also physical pixels) rather
than WPF's `SystemParameters.WorkArea` (DPI-virtualized to the primary monitor) so the
two never get mixed under non-100% scaling.

## GPU aggregation

`GpuMonitorService.Aggregate` (in `Services/GpuMonitorService.cs`) is a pure, testable
static method: given raw per-engine utilization samples and a LUID→adapter mapping, it
takes the **max** utilization across engine types per adapter (not a sum — engines run
concurrently, so summing overstates load), and either uses the explicitly selected
adapter or auto-selects the one currently doing the most work. See
`GpuMonitorServiceTests` for the exact scenarios this covers.
