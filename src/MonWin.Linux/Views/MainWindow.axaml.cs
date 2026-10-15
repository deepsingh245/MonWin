using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SystemMonitor.Models;
using SystemMonitor.Services;
using SystemMonitor.ViewModels;

namespace MonWin.Linux.Views;

/// <summary>
/// The single window that shows live metrics: a tiny borderless overlay, or the larger detailed
/// card (same window, two presentations). It floats along an edge of the work area; Linux has
/// no taskbar to dock into (see LinuxPanelService), and whether the desktop honors the requested
/// position or always-on-top is up to the window manager / compositor.
/// </summary>
public partial class MainWindow : Window
{
    // Pixels the pointer must travel before a press counts as a drag instead of a click.
    private const double DragThresholdPixels = 4;

    // Corner-grip sensitivity: pixels of drag per 1.0 of scale change.
    private const double ResizeDragSensitivity = 150.0;

    private readonly MainViewModel _viewModel = null!;
    private readonly IStartupService _startupService = null!;
    private bool _isVisible;
    private bool _pressed;
    private bool _dragged;
    private PixelPoint _pressScreenPoint;
    private PixelPoint _pressWindowPosition;
    private double _scale = 1.0;

    // Required by the XAML loader/previewer; the app always uses the injected constructor.
    public MainWindow()
    {
        InitializeComponent();
    }

    internal MainWindow(MainViewModel viewModel, IStartupService startupService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _startupService = startupService;
        DataContext = viewModel;

        Card.PointerPressed += OnCardPointerPressed;
        Card.PointerMoved += OnCardPointerMoved;
        Card.PointerReleased += OnCardPointerReleased;
        CardMenu.Opening += (_, _) =>
        {
            PauseMenuItem.IsChecked = _viewModel.IsPaused;
            StartupMenuItem.IsChecked = _startupService.IsEnabled();
        };

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplySettings();
    }

    public void ShowOverlay()
    {
        _viewModel.IsDetailed = false;
        Present();
    }

    public void ShowDetailed()
    {
        _viewModel.IsDetailed = true;
        Present();
        Activate();
    }

    public void ToggleVisible()
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

    private void Present()
    {
        Card.Classes.Set("detailed", _viewModel.IsDetailed);
        Show();
        _isVisible = true;
        // SizeToContent resizes the window on the next layout pass, so position afterwards.
        Dispatcher.UIThread.Post(Reposition, DispatcherPriority.Background);
    }

    private void Reposition()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var workArea = new ScreenRect(area.X, area.Y, area.Right, area.Bottom);
        var width = (int)Math.Ceiling(Bounds.Width * RenderScaling);
        var height = (int)Math.Ceiling(Bounds.Height * RenderScaling);

        if (_viewModel.IsDetailed)
        {
            Position = new PixelPoint(workArea.Left + (workArea.Width - width) / 2, workArea.Top + (workArea.Height - height) / 2);
            return;
        }

        var s = _viewModel.Settings;
        var taskbar = _viewModel.TaskbarService;
        var (x, y) = taskbar.ComputeOverlayPosition(
            taskbar.GetTaskbarInfo(), workArea, s.Position, s.PositionOffsetX, s.PositionOffsetY, s.CustomX, s.CustomY, width, height);
        Position = new PixelPoint(x, y);
    }

    private void ApplySettings()
    {
        _scale = AppSettings.ClampOverlayScale(_viewModel.Settings.OverlayScale);
        ScaleHost.LayoutTransform = new ScaleTransform(_scale, _scale);
        ToolTip.SetTip(Card, _viewModel.Settings.ShowTooltip ? _viewModel.TooltipText : null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Settings):
                ApplySettings();
                if (_isVisible)
                {
                    Dispatcher.UIThread.Post(Reposition, DispatcherPriority.Background);
                }

                break;
            case nameof(MainViewModel.TooltipText):
                ToolTip.SetTip(Card, _viewModel.Settings.ShowTooltip ? _viewModel.TooltipText : null);
                break;
        }
    }

    // Dragging is done by moving the window ourselves (rather than asking the window manager to
    // start a move) so a plain click can be told apart from a drag, identically on every desktop.
    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Card).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = true;
        _dragged = false;
        _pressScreenPoint = Card.PointToScreen(e.GetPosition(Card));
        _pressWindowPosition = Position;
        e.Pointer.Capture(Card);
    }

    private void OnCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        var current = Card.PointToScreen(e.GetPosition(Card));
        var dx = current.X - _pressScreenPoint.X;
        var dy = current.Y - _pressScreenPoint.Y;
        if (!_dragged && Math.Abs(dx) < DragThresholdPixels && Math.Abs(dy) < DragThresholdPixels)
        {
            return;
        }

        _dragged = true;
        Position = new PixelPoint(_pressWindowPosition.X + dx, _pressWindowPosition.Y + dy);
    }

    private void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Middle)
        {
            ToggleVisible();
            return;
        }

        if (!_pressed || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        _pressed = false;
        e.Pointer.Capture(null);

        if (_dragged)
        {
            if (!_viewModel.IsDetailed)
            {
                _viewModel.SaveDraggedPosition(Position.X, Position.Y);
            }
        }
        else if (!_viewModel.IsDetailed)
        {
            ShowDetailed();
        }
    }

    private void OnResizeThumbDragDelta(object? sender, VectorEventArgs e)
    {
        var delta = (e.Vector.X + e.Vector.Y) / 2.0 / ResizeDragSensitivity;
        _scale = AppSettings.ClampOverlayScale(_scale + delta);
        ScaleHost.LayoutTransform = new ScaleTransform(_scale, _scale);
        Dispatcher.UIThread.Post(Reposition, DispatcherPriority.Background);
    }

    private void OnResizeThumbDragCompleted(object? sender, VectorEventArgs e) => _viewModel.SaveScale(_scale);

    private void OnResizeThumbDoubleTapped(object? sender, TappedEventArgs e)
    {
        _scale = 1.0;
        ScaleHost.LayoutTransform = new ScaleTransform(1.0, 1.0);
        _viewModel.SaveScale(1.0);
        Dispatcher.UIThread.Post(Reposition, DispatcherPriority.Background);
    }

    private void OnOpenDetailedClick(object? sender, RoutedEventArgs e) => ShowDetailed();

    private void OnShowCompactClick(object? sender, RoutedEventArgs e) => ShowOverlay();

    private void OnOpenSettingsClick(object? sender, RoutedEventArgs e) => (Application.Current as App)?.OpenSettings();

    private void OnOpenAboutClick(object? sender, RoutedEventArgs e) => (Application.Current as App)?.OpenAbout();

    private void OnTogglePauseClick(object? sender, RoutedEventArgs e) => _viewModel.TogglePauseCommand.Execute(null);

    private void OnToggleStartupClick(object? sender, RoutedEventArgs e) => _viewModel.ToggleStartWithWindowsCommand.Execute(null);

    private void OnExitClick(object? sender, RoutedEventArgs e) => (Application.Current as App)?.ExitApplication();
}
