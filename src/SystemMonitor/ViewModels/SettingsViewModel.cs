using SystemMonitor.Models;
using SystemMonitor.Services;
using SystemMonitor.ViewModels.Base;

namespace SystemMonitor.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly IGpuMonitor _gpuMonitor;

    private bool _startWithWindows;
    private bool _startMinimized;
    private int _updateIntervalMs;
    private int _historySeconds;
    private DisplayMode _displayMode;
    private bool _showCpu;
    private bool _showRam;
    private bool _showGpu;
    private bool _showNetwork;
    private bool _showDisk;
    private OverlayPosition _position;
    private int _customX;
    private int _customY;
    private AppTheme _theme;
    private bool _showTooltip;
    private bool _clickThrough;
    private int? _selectedGpuAdapterIndex;

    public int[] UpdateIntervalOptionsMs => AppSettings.ValidUpdateIntervalsMs;
    public int[] HistoryOptionsSeconds => AppSettings.ValidHistorySeconds;
    public IReadOnlyList<GpuAdapterInfo> GpuAdapters => _gpuMonitor.Adapters;

    public bool StartWithWindows { get => _startWithWindows; set => SetProperty(ref _startWithWindows, value); }
    public bool StartMinimized { get => _startMinimized; set => SetProperty(ref _startMinimized, value); }
    public int UpdateIntervalMs { get => _updateIntervalMs; set => SetProperty(ref _updateIntervalMs, value); }
    public int HistorySeconds { get => _historySeconds; set => SetProperty(ref _historySeconds, value); }
    public DisplayMode DisplayMode { get => _displayMode; set => SetProperty(ref _displayMode, value); }
    public bool ShowCpu { get => _showCpu; set => SetProperty(ref _showCpu, value); }
    public bool ShowRam { get => _showRam; set => SetProperty(ref _showRam, value); }
    public bool ShowGpu { get => _showGpu; set => SetProperty(ref _showGpu, value); }
    public bool ShowNetwork { get => _showNetwork; set => SetProperty(ref _showNetwork, value); }
    public bool ShowDisk { get => _showDisk; set => SetProperty(ref _showDisk, value); }
    public OverlayPosition Position { get => _position; set => SetProperty(ref _position, value); }
    public int CustomX { get => _customX; set => SetProperty(ref _customX, value); }
    public int CustomY { get => _customY; set => SetProperty(ref _customY, value); }
    public AppTheme Theme { get => _theme; set => SetProperty(ref _theme, value); }
    public bool ShowTooltip { get => _showTooltip; set => SetProperty(ref _showTooltip, value); }
    public bool ClickThrough { get => _clickThrough; set => SetProperty(ref _clickThrough, value); }
    public int? SelectedGpuAdapterIndex { get => _selectedGpuAdapterIndex; set => SetProperty(ref _selectedGpuAdapterIndex, value); }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event EventHandler? RequestClose;

    public SettingsViewModel(ISettingsService settingsService, IStartupService startupService, IGpuMonitor gpuMonitor)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _gpuMonitor = gpuMonitor;

        LoadFrom(_settingsService.Current);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    private void LoadFrom(AppSettings s)
    {
        _startWithWindows = s.StartWithWindows;
        _startMinimized = s.StartMinimized;
        _updateIntervalMs = s.UpdateIntervalMs;
        _historySeconds = s.HistorySeconds;
        _displayMode = s.DisplayMode;
        _showCpu = s.ShowCpu;
        _showRam = s.ShowRam;
        _showGpu = s.ShowGpu;
        _showNetwork = s.ShowNetwork;
        _showDisk = s.ShowDisk;
        _position = s.Position;
        _customX = s.CustomX;
        _customY = s.CustomY;
        _theme = s.Theme;
        _showTooltip = s.ShowTooltip;
        _clickThrough = s.ClickThrough;
        _selectedGpuAdapterIndex = s.SelectedGpuAdapterIndex;
    }

    private void Save()
    {
        var settings = new AppSettings
        {
            StartWithWindows = StartWithWindows,
            StartMinimized = StartMinimized,
            UpdateIntervalMs = UpdateIntervalMs,
            HistorySeconds = HistorySeconds,
            DisplayMode = DisplayMode,
            ShowCpu = ShowCpu,
            ShowRam = ShowRam,
            ShowGpu = ShowGpu,
            ShowNetwork = ShowNetwork,
            ShowDisk = ShowDisk,
            Position = Position,
            CustomX = CustomX,
            CustomY = CustomY,
            Theme = Theme,
            ShowTooltip = ShowTooltip,
            ClickThrough = ClickThrough,
            SelectedGpuAdapterIndex = SelectedGpuAdapterIndex,
        };

        _startupService.SetEnabled(StartWithWindows);
        _settingsService.Save(settings);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
