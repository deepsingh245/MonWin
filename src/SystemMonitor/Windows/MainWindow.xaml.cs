using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    private const int WM_SETTINGCHANGE = 0x001A;

    private bool _isVisible = true;

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
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == (int)TaskbarCreatedMessage || msg is WM_DISPLAYCHANGE or WM_DPICHANGED or WM_SETTINGCHANGE)
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
            TaskbarInterop.SetWindowPos(hwnd, TaskbarInterop.HWND_TOPMOST, 0, 0, 0, 0,
                TaskbarInterop.SWP_NOSIZE | TaskbarInterop.SWP_NOACTIVATE);
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

    private void OnCardLeftClick(object sender, MouseButtonEventArgs e)
    {
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

    private void OnCloseDetailedClick(object sender, RoutedEventArgs e) => ShowOverlay();

    private void OnOpenDetailedClick(object sender, RoutedEventArgs e) => ShowDetailed();

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e) => (System.Windows.Application.Current as App)?.OpenSettings();

    private void OnOpenAboutClick(object sender, RoutedEventArgs e) => (System.Windows.Application.Current as App)?.OpenAbout();

    private void OnExitClick(object sender, RoutedEventArgs e) => (System.Windows.Application.Current as App)?.ExitApplication();
}
