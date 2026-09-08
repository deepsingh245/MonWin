using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SystemMonitor.Models;
using SystemMonitor.Native;
using SystemMonitor.ViewModels;

namespace SystemMonitor.Windows;

/// <summary>
/// The single application window: a tiny chromeless "Overlay" presentation anchored
/// near the taskbar, and a larger chromeless "Detailed" presentation — both borderless
/// so window-chrome (WindowStyle/AllowsTransparency) never needs to change after the
/// window handle exists. Owns all taskbar-tracking / DPI / Explorer-restart handling.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private static readonly uint TaskbarCreatedMessage = TaskbarInterop.RegisterWindowMessage("TaskbarCreated");
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_DPICHANGED = 0x02E0;

    private bool _isVisible = true;
    private bool _suppressNextClick;
    private DispatcherTimer? _topmostTimer;

    // Corner-grip resize sensitivity: pixels of drag per 1.0 of scale change.
    private const double ResizeDragSensitivity = 150.0;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = TaskbarInterop.GetWindowLongPtr(hwnd, TaskbarInterop.GWL_EXSTYLE).ToInt64();
        exStyle |= TaskbarInterop.WS_EX_TOOLWINDOW | TaskbarInterop.WS_EX_NOACTIVATE;
        TaskbarInterop.SetWindowLongPtr(hwnd, TaskbarInterop.GWL_EXSTYLE, new nint(exStyle));
        ApplyClickThrough();

        var hwndSource = HwndSource.FromHwnd(hwnd);
        hwndSource?.AddHook(WndProc);

        // The taskbar (Shell_TrayWnd) is itself an always-on-top window, and focusing/
        // clicking it can restack it above ours within the topmost band — a one-time
        // SetWindowPos(HWND_TOPMOST) at show-time doesn't survive that. Re-assert on a
        // steady cadence so we pop back above it within a second rather than staying
        // stuck behind it (and unclickable) until the window is hidden/shown again.
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _topmostTimer.Tick += (_, _) =>
        {
            if (IsVisible)
            {
                SetTopmost();
            }
        };
        _topmostTimer.Start();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // Deliberately narrow: WM_SETTINGCHANGE is broadcast for many unrelated system
        // settings (not just display/taskbar layout) and re-querying the taskbar on every
        // one of those caused visible positional jitter with no real trigger behind it.
        // TaskbarCreated/WM_DISPLAYCHANGE/WM_DPICHANGED already cover every case that
        // actually changes taskbar geometry.
        if (msg == (int)TaskbarCreatedMessage || msg is WM_DISPLAYCHANGE or WM_DPICHANGED)
        {
            Dispatcher.BeginInvoke(RepositionWindow);
        }

        return nint.Zero;
    }

    public void ShowOverlay()
    {
        OverlayContent.Visibility = Visibility.Visible;
        DetailedContent.Visibility = Visibility.Collapsed;
        CardBorder.Style = (Style)FindResource("MonWin.Border.Card");
        _viewModel.IsDetailed = false;
        Show();
        _isVisible = true;
        UpdateLayout();
        RepositionWindow();
        SetTopmost();
    }

    public void ShowDetailed()
    {
        OverlayContent.Visibility = Visibility.Collapsed;
        DetailedContent.Visibility = Visibility.Visible;
        CardBorder.Style = (Style)FindResource("MonWin.Border.DetailedCard");
        _viewModel.IsDetailed = true;
        Show();
        _isVisible = true;
        UpdateLayout();
        CenterOnPrimaryScreen();
        SetTopmost();
        Activate();
    }

    private void ToggleVisible()
    {
        if (_isVisible)
        {
            Hide();
            _isVisible = false;
        }
        else
        {
            ShowOverlay();
        }
    }

    private void RepositionWindow()
    {
        if (_viewModel.IsDetailed)
        {
            return;
        }

        var taskbarService = _viewModel.TaskbarService;
        var settings = _viewModel.Settings;
        var taskbarInfo = taskbarService.GetTaskbarInfo();

        // taskbarInfo.Bounds comes from raw Win32 (physical pixels). SystemParameters.WorkArea
        // is DPI-virtualized to the primary monitor's scale, which does NOT match physical
        // pixels once any monitor runs above 100% scaling — so we use Forms.Screen's
        // physical-pixel working area here instead, to stay in the same coordinate space.
        var screenWorkArea = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
            ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        var workAreaRect = new ScreenRect(screenWorkArea.Left, screenWorkArea.Top, screenWorkArea.Right, screenWorkArea.Bottom);

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)(ActualWidth * dpi.DpiScaleX);
        var height = (int)(ActualHeight * dpi.DpiScaleY);

        var (x, y) = taskbarService.ComputeOverlayPosition(
            taskbarInfo, workAreaRect, settings.Position,
            settings.PositionOffsetX, settings.PositionOffsetY,
            settings.CustomX, settings.CustomY, width, height);

        Left = x / dpi.DpiScaleX;
        Top = y / dpi.DpiScaleY;
    }

    private void CenterOnPrimaryScreen()
    {
        var workArea = System.Windows.SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + (workArea.Height - ActualHeight) / 2;
    }

    private void SetTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != 0)
        {
            // x/y/cx/cy are ignored (and MUST be, via SWP_NOMOVE|SWP_NOSIZE) — this call
            // only reorders the window in the z-order, it must never reposition it. Omitting
            // SWP_NOMOVE here previously snapped the window to (0,0) on every call, which is
            // exactly why dragging it appeared to "stick back to top-left" once the periodic
            // topmost-reassertion timer fired.
            TaskbarInterop.SetWindowPos(hwnd, TaskbarInterop.HWND_TOPMOST, 0, 0, 0, 0,
                TaskbarInterop.SWP_NOMOVE | TaskbarInterop.SWP_NOSIZE | TaskbarInterop.SWP_NOACTIVATE);
        }
    }

    private void ApplyClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = TaskbarInterop.GetWindowLongPtr(hwnd, TaskbarInterop.GWL_EXSTYLE).ToInt64();
        if (_viewModel.Settings.ClickThrough && !_viewModel.IsDetailed)
        {
            exStyle |= TaskbarInterop.WS_EX_TRANSPARENT;
        }
        else
        {
            exStyle &= ~TaskbarInterop.WS_EX_TRANSPARENT;
        }

        TaskbarInterop.SetWindowLongPtr(hwnd, TaskbarInterop.GWL_EXSTYLE, new nint(exStyle));
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Settings))
        {
            ApplyClickThrough();
            if (!_viewModel.IsDetailed)
            {
                RepositionWindow();
            }
        }
    }

    /// <summary>Walks up from <paramref name="element"/> (stopping at CardBorder) looking
    /// for a Button ancestor, so drag-to-move can exclude clicks on real buttons.</summary>
    private bool HasAncestorButton(DependencyObject element)
    {
        var current = element;
        while (current is not null && current != CardBorder)
        {
            if (current is System.Windows.Controls.Button)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// Distinguishes a plain click (opens Detailed) from a drag (moves the overlay).
    /// DragMove() blocks until the mouse button is released and only actually moves the
    /// window once the OS's own drag threshold is exceeded, so a real click still leaves
    /// Left/Top unchanged — that's the signal used below to tell the two apart.
    /// </summary>
    private void OnCardPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.Settings.ClickThrough)
        {
            return;
        }

        // PreviewMouseLeftButtonDown tunnels from CardBorder down to whatever was
        // actually clicked, so without this check, clicking a button inside the card
        // (e.g. the Detailed view's "✕" close button) would trigger DragMove() first
        // and swallow the click before it ever reached the button.
        if (e.OriginalSource is DependencyObject source && HasAncestorButton(source))
        {
            return;
        }

        var startLeft = Left;
        var startTop = Top;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var moved = Math.Abs(Left - startLeft) > 0.5 || Math.Abs(Top - startTop) > 0.5;
        if (!moved)
        {
            return;
        }

        _suppressNextClick = true;
        if (!_viewModel.IsDetailed)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var physicalX = (int)Math.Round(Left * dpi.DpiScaleX);
            var physicalY = (int)Math.Round(Top * dpi.DpiScaleY);
            _viewModel.SaveDraggedPosition(physicalX, physicalY);
        }
    }

    private void OnCardLeftClick(object sender, MouseButtonEventArgs e)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        if (!_viewModel.IsDetailed)
        {
            ShowDetailed();
        }
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            ToggleVisible();
        }
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        var delta = (e.HorizontalChange + e.VerticalChange) / 2.0 / ResizeDragSensitivity;
        var newScale = AppSettings.ClampOverlayScale(CardScaleTransform.ScaleX + delta);
        CardScaleTransform.ScaleX = newScale;
        CardScaleTransform.ScaleY = newScale;
        UpdateLayout();
        if (_viewModel.IsDetailed)
        {
            CenterOnPrimaryScreen();
        }
        else
        {
            RepositionWindow();
        }
    }

    private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e) =>
        _viewModel.SaveScale(CardScaleTransform.ScaleX);

    private void OnResizeThumbMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        CardScaleTransform.ScaleX = 1.0;
        CardScaleTransform.ScaleY = 1.0;
        UpdateLayout();
        if (_viewModel.IsDetailed)
        {
            CenterOnPrimaryScreen();
        }
        else
        {
            RepositionWindow();
        }

        _viewModel.SaveScale(1.0);
    }

    private void OnCloseDetailedClick(object sender, RoutedEventArgs e) => ShowOverlay();

    private void OnOpenDetailedClick(object sender, RoutedEventArgs e) => ShowDetailed();

    private void OnShowCompactClick(object sender, RoutedEventArgs e) => ShowOverlay();

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e) => (System.Windows.Application.Current as App)?.OpenSettings();

    private void OnOpenAboutClick(object sender, RoutedEventArgs e) => (System.Windows.Application.Current as App)?.OpenAbout();

    private void OnExitClick(object sender, RoutedEventArgs e) => (System.Windows.Application.Current as App)?.ExitApplication();
}
