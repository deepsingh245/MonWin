using System.Windows.Threading;
using static System.FormattableString;
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
    private NetworkMetrics _network = NetworkMetrics.Unavailable;
    private DiskMetrics _disk = DiskMetrics.Unavailable;
    private double[] _cpuHistory = [];
    private double[] _memoryHistory = [];
    private double[] _gpuHistory = [];
    private double[] _networkHistory = [];
    private double[] _diskHistory = [];

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

    public NetworkMetrics Network
    {
        get => _network;
        private set => SetProperty(ref _network, value);
    }

    public DiskMetrics Disk
    {
        get => _disk;
        private set => SetProperty(ref _disk, value);
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

    public double[] NetworkHistory
    {
        get => _networkHistory;
        private set => SetProperty(ref _networkHistory, value);
    }

    public double[] DiskHistory
    {
        get => _diskHistory;
        private set => SetProperty(ref _diskHistory, value);
    }

    // All display strings use the invariant culture so the compact bar's width (and the
    // decimal separator) never changes with the user's regional settings.
    public string CpuValueText => Cpu.IsAvailable ? Invariant($"{Cpu.UsagePercent:F0}%") : "N/A";
    public string CpuSecondaryText => Cpu.IsAvailable && Cpu.FrequencyMhz is { } mhz ? Invariant($"{mhz / 1000.0:F2} GHz") : string.Empty;
    public string ProcessorInfoText => Cpu.IsAvailable ? Invariant($"{Cpu.ProcessorName} · {Cpu.LogicalCoreCount} threads") : string.Empty;

    public string RamValueText => Memory.IsAvailable ? Invariant($"{Memory.UsagePercent:F0}%") : "N/A";
    public string RamSecondaryText => Memory.IsAvailable ? Invariant($"{Memory.UsedGigabytes:F1} / {Memory.TotalGigabytes:F1} GB") : string.Empty;

    public string GpuValueText => Gpu.IsAvailable && Gpu.UsagePercent is { } pct ? Invariant($"{pct:F0}%") : "N/A";
    public string GpuSecondaryText => Gpu.IsAvailable && Gpu.UsedMemoryGigabytes is { } used
        ? Gpu.TotalMemoryGigabytes is { } total ? Invariant($"{used:F2} / {total:F1} GB") : Invariant($"{used:F2} GB")
        : string.Empty;
    public string GpuName => Gpu.IsAvailable ? Gpu.Name : "N/A";

    private bool NetInBits => Settings.NetworkUnit == NetworkUnit.Bits;
    private string Rate(double bytesPerSecond) => RateFormatter.Format(bytesPerSecond, NetInBits);

    public string NetValueText => Network.IsAvailable
        ? $"↓{Rate(Network.DownloadBytesPerSecond)} ↑{Rate(Network.UploadBytesPerSecond)}"
        : "N/A";
    public string NetTotalText => Network.IsAvailable
        ? Rate(Network.DownloadBytesPerSecond + Network.UploadBytesPerSecond)
        : "N/A";
    public string NetSecondaryText => Network.IsAvailable
        ? $"Down {Rate(Network.DownloadBytesPerSecond)} · Up {Rate(Network.UploadBytesPerSecond)}"
        : string.Empty;

    public string DiskValueText => Disk.IsAvailable ? Invariant($"{Disk.ActivePercent:F0}%") : "N/A";
    public string DiskSecondaryText => Disk.IsAvailable
        ? $"R {RateFormatter.Format(Disk.ReadBytesPerSecond)} · W {RateFormatter.Format(Disk.WriteBytesPerSecond)}"
        : string.Empty;

    public MetricSeverity CpuSeverity =>
        ThresholdEvaluator.Evaluate(Cpu.IsAvailable ? Cpu.UsagePercent : null, Settings.CpuWarn, Settings.CpuCrit);
    public MetricSeverity RamSeverity =>
        ThresholdEvaluator.Evaluate(Memory.IsAvailable ? Memory.UsagePercent : null, Settings.RamWarn, Settings.RamCrit);
    public MetricSeverity GpuSeverity =>
        ThresholdEvaluator.Evaluate(Gpu.IsAvailable ? Gpu.UsagePercent : null, Settings.GpuWarn, Settings.GpuCrit);
    public MetricSeverity DiskSeverity =>
        ThresholdEvaluator.Evaluate(Disk.IsAvailable ? Disk.ActivePercent : null, Settings.DiskWarn, Settings.DiskCrit);

    /// <summary>Hover summary of every enabled metric, so the details are a glance away without opening the detailed view.</summary>
    public string TooltipText
    {
        get
        {
            var s = Settings;
            var lines = new List<string>();
            if (s.ShowCpu) lines.Add(Line("CPU", CpuValueText, CpuSecondaryText));
            if (s.ShowRam) lines.Add(Line("RAM", RamValueText, RamSecondaryText));
            if (s.ShowGpu) lines.Add(Line("GPU", GpuValueText, Gpu.IsAvailable ? $"{GpuSecondaryText} · {Gpu.Name}".Trim(' ', '·') : string.Empty));
            if (s.ShowNetwork) lines.Add(Line("Network", NetTotalText, NetSecondaryText));
            if (s.ShowDisk) lines.Add(Line("Disk", DiskValueText, DiskSecondaryText));
            lines.Add(string.Empty);
            lines.Add("Click: details · Right-click: menu · Drag: move · Corner grip: resize");
            return string.Join(Environment.NewLine, lines);

            static string Line(string label, string value, string secondary) =>
                string.IsNullOrEmpty(secondary) ? $"{label}  {value}" : $"{label}  {value}   ({secondary})";
        }
    }

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
            RaiseSettingsDependentProperties();
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
            Network = snapshot.Network;
            Disk = snapshot.Disk;
            CpuHistory = _monitor.CpuHistory.ToArray();
            MemoryHistory = _monitor.MemoryHistory.ToArray();
            GpuHistory = _monitor.GpuHistory.ToArray();
            NetworkHistory = _monitor.NetworkHistory.ToArray();
            DiskHistory = _monitor.DiskHistory.ToArray();

            OnPropertyChanged(nameof(CpuValueText));
            OnPropertyChanged(nameof(CpuSecondaryText));
            OnPropertyChanged(nameof(ProcessorInfoText));
            OnPropertyChanged(nameof(RamValueText));
            OnPropertyChanged(nameof(RamSecondaryText));
            OnPropertyChanged(nameof(GpuValueText));
            OnPropertyChanged(nameof(GpuSecondaryText));
            OnPropertyChanged(nameof(GpuName));
            OnPropertyChanged(nameof(DiskValueText));
            OnPropertyChanged(nameof(DiskSecondaryText));
            RaiseSettingsDependentProperties();
        });
    }

    /// <summary>Properties computed from both the latest snapshot and the settings (thresholds, units, visibility).</summary>
    private void RaiseSettingsDependentProperties()
    {
        OnPropertyChanged(nameof(NetValueText));
        OnPropertyChanged(nameof(NetTotalText));
        OnPropertyChanged(nameof(NetSecondaryText));
        OnPropertyChanged(nameof(CpuSeverity));
        OnPropertyChanged(nameof(RamSeverity));
        OnPropertyChanged(nameof(GpuSeverity));
        OnPropertyChanged(nameof(DiskSeverity));
        OnPropertyChanged(nameof(TooltipText));
    }

    public void Dispose()
    {
        _monitor.SnapshotUpdated -= OnSnapshotUpdated;
    }
}
