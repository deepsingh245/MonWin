using System.Windows.Threading;
using SystemMonitor.Models;
using SystemMonitor.Services;
using SystemMonitor.ViewModels.Base;

namespace SystemMonitor.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISystemMonitorService _monitor;
    private readonly ISettingsService _settingsService;
    private readonly ITaskbarService _taskbarService;
    private readonly IStartupService _startupService;
    private readonly Dispatcher _dispatcher;

    private bool _isDetailed;
    private bool _isPaused;
    private CpuMetrics _cpu = CpuMetrics.Unavailable;
    private MemoryMetrics _memory = MemoryMetrics.Unavailable;
    private GpuMetrics _gpu = GpuMetrics.Unavailable;
    private double[] _cpuHistory = [];
    private double[] _memoryHistory = [];
    private double[] _gpuHistory = [];

    public AppSettings Settings => _settingsService.Current;

    public bool IsDetailed
    {
        get => _isDetailed;
        set => SetProperty(ref _isDetailed, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    public CpuMetrics Cpu
    {
        get => _cpu;
        private set => SetProperty(ref _cpu, value);
    }

    public MemoryMetrics Memory
    {
        get => _memory;
        private set => SetProperty(ref _memory, value);
    }

    public GpuMetrics Gpu
    {
        get => _gpu;
        private set => SetProperty(ref _gpu, value);
    }

    public double[] CpuHistory
    {
        get => _cpuHistory;
        private set => SetProperty(ref _cpuHistory, value);
    }

    public double[] MemoryHistory
    {
        get => _memoryHistory;
        private set => SetProperty(ref _memoryHistory, value);
    }

    public double[] GpuHistory
    {
        get => _gpuHistory;
        private set => SetProperty(ref _gpuHistory, value);
    }

    public string CpuValueText => Cpu.IsAvailable ? $"{Cpu.UsagePercent:F0}%" : "N/A";
    public string CpuSecondaryText => Cpu.IsAvailable && Cpu.FrequencyMhz is { } mhz ? $"{mhz / 1000.0:F2} GHz" : string.Empty;
    public string ProcessorInfoText => Cpu.IsAvailable ? $"{Cpu.ProcessorName} · {Cpu.LogicalCoreCount} threads" : string.Empty;

    public string RamValueText => Memory.IsAvailable ? $"{Memory.UsagePercent:F0}%" : "N/A";
    public string RamSecondaryText => Memory.IsAvailable ? $"{Memory.UsedGigabytes:F1} / {Memory.TotalGigabytes:F1} GB" : string.Empty;

    public string GpuValueText => Gpu.IsAvailable && Gpu.UsagePercent is { } pct ? $"{pct:F0}%" : "N/A";
    public string GpuSecondaryText => Gpu.IsAvailable && Gpu.UsedMemoryGigabytes is { } used
        ? Gpu.TotalMemoryGigabytes is { } total ? $"{used:F2} / {total:F1} GB" : $"{used:F2} GB"
        : string.Empty;
    public string GpuName => Gpu.IsAvailable ? Gpu.Name : "N/A";

    public RelayCommand ShowDetailedCommand { get; }
    public RelayCommand ShowOverlayCommand { get; }
    public RelayCommand TogglePauseCommand { get; }
    public RelayCommand ToggleStartWithWindowsCommand { get; }

    public MainViewModel(
        ISystemMonitorService monitor,
        ISettingsService settingsService,
        ITaskbarService taskbarService,
        IStartupService startupService,
        Dispatcher dispatcher)
    {
        _monitor = monitor;
        _settingsService = settingsService;
        _taskbarService = taskbarService;
        _startupService = startupService;
        _dispatcher = dispatcher;

        _monitor.SnapshotUpdated += OnSnapshotUpdated;
        _settingsService.SettingsChanged += (_, settings) =>
        {
            _monitor.ApplySettings(settings);
            OnPropertyChanged(nameof(Settings));
        };

        ShowDetailedCommand = new RelayCommand(() => IsDetailed = true);
        ShowOverlayCommand = new RelayCommand(() => IsDetailed = false);
        TogglePauseCommand = new RelayCommand(TogglePause);
        ToggleStartWithWindowsCommand = new RelayCommand(ToggleStartWithWindows);
    }

    public ITaskbarService TaskbarService => _taskbarService;

    /// <summary>Called after the user drags the overlay to a new spot — switches Position to
    /// Custom and remembers the drop location (in physical screen pixels) instead of snapping
    /// back to the Left/Center/Right taskbar anchor.</summary>
    public void SaveDraggedPosition(int physicalX, int physicalY)
    {
        var settings = _settingsService.Current;
        settings.Position = OverlayPosition.Custom;
        settings.CustomX = physicalX;
        settings.CustomY = physicalY;
        settings.PositionOffsetX = 0;
        settings.PositionOffsetY = 0;
        _settingsService.Save(settings);
    }

    /// <summary>Called after the user finishes dragging the resize grip.</summary>
    public void SaveScale(double scale)
    {
        var settings = _settingsService.Current;
        settings.OverlayScale = AppSettings.ClampOverlayScale(scale);
        _settingsService.Save(settings);
    }

    private void TogglePause()
    {
        if (_monitor.IsPaused)
        {
            _monitor.Resume();
        }
        else
        {
            _monitor.Pause();
        }

        IsPaused = _monitor.IsPaused;
    }

    private void ToggleStartWithWindows()
    {
        var enabled = !_startupService.IsEnabled();
        _startupService.SetEnabled(enabled);
        var settings = _settingsService.Current;
        settings.StartWithWindows = enabled;
        _settingsService.Save(settings);
        OnPropertyChanged(nameof(Settings));
    }

    private void OnSnapshotUpdated(object? sender, MonitorSnapshot snapshot)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Cpu = snapshot.Cpu;
            Memory = snapshot.Memory;
            Gpu = snapshot.Gpu;
            CpuHistory = _monitor.CpuHistory.ToArray();
            MemoryHistory = _monitor.MemoryHistory.ToArray();
            GpuHistory = _monitor.GpuHistory.ToArray();

            OnPropertyChanged(nameof(CpuValueText));
            OnPropertyChanged(nameof(CpuSecondaryText));
            OnPropertyChanged(nameof(ProcessorInfoText));
            OnPropertyChanged(nameof(RamValueText));
            OnPropertyChanged(nameof(RamSecondaryText));
            OnPropertyChanged(nameof(GpuValueText));
            OnPropertyChanged(nameof(GpuSecondaryText));
            OnPropertyChanged(nameof(GpuName));
        });
    }

    public void Dispose()
    {
        _monitor.SnapshotUpdated -= OnSnapshotUpdated;
    }
}
