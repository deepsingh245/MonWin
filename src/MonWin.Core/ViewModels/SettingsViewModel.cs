using SystemMonitor.Models;
using SystemMonitor.Services;
using SystemMonitor.ViewModels.Base;

namespace SystemMonitor.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly IGpuMonitor _gpuMonitor;
    private readonly IHotkeyService _hotkeyService;
    private readonly IColorPickerService _colorPicker;

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
    private bool _graphCpu;
    private bool _graphRam;
    private bool _graphGpu;
    private bool _graphNetwork;
    private bool _graphDisk;
    private NetworkUnit _networkUnit;
    private int _cpuWarn, _cpuCrit, _ramWarn, _ramCrit, _gpuWarn, _gpuCrit, _diskWarn, _diskCrit;
    private bool _trayShowsCpu;
    private string _toggleHotkey = string.Empty;
    private OverlayPosition _position;
    private int _customX;
    private int _customY;
    private AppTheme _theme;
    private bool _showTooltip;
    private bool _clickThrough;
    private int? _selectedGpuAdapterIndex;
    private string? _accentColorHex;

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
    public bool GraphCpu { get => _graphCpu; set => SetProperty(ref _graphCpu, value); }
    public bool GraphRam { get => _graphRam; set => SetProperty(ref _graphRam, value); }
    public bool GraphGpu { get => _graphGpu; set => SetProperty(ref _graphGpu, value); }
    public bool GraphNetwork { get => _graphNetwork; set => SetProperty(ref _graphNetwork, value); }
    public bool GraphDisk { get => _graphDisk; set => SetProperty(ref _graphDisk, value); }
    public NetworkUnit NetworkUnit { get => _networkUnit; set => SetProperty(ref _networkUnit, value); }
    public int CpuWarn { get => _cpuWarn; set => SetProperty(ref _cpuWarn, value); }
    public int CpuCrit { get => _cpuCrit; set => SetProperty(ref _cpuCrit, value); }
    public int RamWarn { get => _ramWarn; set => SetProperty(ref _ramWarn, value); }
    public int RamCrit { get => _ramCrit; set => SetProperty(ref _ramCrit, value); }
    public int GpuWarn { get => _gpuWarn; set => SetProperty(ref _gpuWarn, value); }
    public int GpuCrit { get => _gpuCrit; set => SetProperty(ref _gpuCrit, value); }
    public int DiskWarn { get => _diskWarn; set => SetProperty(ref _diskWarn, value); }
    public int DiskCrit { get => _diskCrit; set => SetProperty(ref _diskCrit, value); }
    public bool TrayShowsCpu { get => _trayShowsCpu; set => SetProperty(ref _trayShowsCpu, value); }

    public string ToggleHotkey
    {
        get => _toggleHotkey;
        set
        {
            if (SetProperty(ref _toggleHotkey, value))
            {
                OnPropertyChanged(nameof(HotkeyStatus));
            }
        }
    }

    /// <summary>
    /// Feedback under the hotkey box: a format error for what's typed now, otherwise the
    /// result of registering the saved shortcut (e.g. another app already owns it).
    /// </summary>
    public string? HotkeyStatus
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ToggleHotkey))
            {
                return "Disabled.";
            }

            if (!HotkeyParser.TryParse(ToggleHotkey, out _, out _))
            {
                return "Use a modifier plus a key, e.g. Ctrl+Alt+M.";
            }

            return ToggleHotkey.Trim() == _settingsService.Current.ToggleHotkey ? _hotkeyService.Status : null;
        }
    }

    public OverlayPosition Position { get => _position; set => SetProperty(ref _position, value); }
    public int CustomX { get => _customX; set => SetProperty(ref _customX, value); }
    public int CustomY { get => _customY; set => SetProperty(ref _customY, value); }
    public AppTheme Theme { get => _theme; set => SetProperty(ref _theme, value); }
    public bool ShowTooltip { get => _showTooltip; set => SetProperty(ref _showTooltip, value); }
    public bool ClickThrough { get => _clickThrough; set => SetProperty(ref _clickThrough, value); }
    public int? SelectedGpuAdapterIndex { get => _selectedGpuAdapterIndex; set => SetProperty(ref _selectedGpuAdapterIndex, value); }
    public string? AccentColorHex { get => _accentColorHex; set => SetProperty(ref _accentColorHex, value); }

    /// <summary>A small, Windows-11-flavored quick-pick palette. "Custom..." opens the
    /// native Windows color picker for anything else.</summary>
    public IReadOnlyList<string> PresetAccentColors { get; } =
        ["#0067C0", "#0F7B0F", "#8764B8", "#CA5010", "#C42B1C", "#00B7C3", "#E3008C", "#69797E"];

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectAccentColorCommand { get; }
    public RelayCommand PickCustomColorCommand { get; }
    public RelayCommand ResetAccentColorCommand { get; }
    public RelayCommand ResetThresholdsCommand { get; }

    public event EventHandler? RequestClose;

    /// <summary>Raised around showing the native color picker, so the owning window can
    /// suspend its click-outside-to-dismiss behavior — otherwise the color dialog
    /// stealing focus would immediately close Settings before the user picks anything.</summary>
    public event EventHandler? ExternalDialogOpening;
    public event EventHandler? ExternalDialogClosed;

    public SettingsViewModel(ISettingsService settingsService, IStartupService startupService, IGpuMonitor gpuMonitor, IHotkeyService hotkeyService, IColorPickerService colorPicker)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _gpuMonitor = gpuMonitor;
        _hotkeyService = hotkeyService;
        _colorPicker = colorPicker;

        LoadFrom(_settingsService.Current);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
        SelectAccentColorCommand = new RelayCommand(p => AccentColorHex = p as string);
        PickCustomColorCommand = new RelayCommand(PickCustomColor);
        ResetAccentColorCommand = new RelayCommand(() => AccentColorHex = null);
        ResetThresholdsCommand = new RelayCommand(ResetThresholds);
    }

    private void ResetThresholds()
    {
        CpuWarn = RamWarn = GpuWarn = DiskWarn = AppSettings.DefaultWarnThreshold;
        CpuCrit = RamCrit = GpuCrit = DiskCrit = AppSettings.DefaultCritThreshold;
    }

    private void PickCustomColor()
    {
        ExternalDialogOpening?.Invoke(this, EventArgs.Empty);
        try
        {
            var picked = _colorPicker.PickColor(AccentColorHex);
            if (picked is not null)
            {
                AccentColorHex = picked;
            }
        }
        finally
        {
            ExternalDialogClosed?.Invoke(this, EventArgs.Empty);
        }
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
        _graphCpu = s.GraphCpu;
        _graphRam = s.GraphRam;
        _graphGpu = s.GraphGpu;
        _graphNetwork = s.GraphNetwork;
        _graphDisk = s.GraphDisk;
        _networkUnit = s.NetworkUnit;
        (_cpuWarn, _cpuCrit) = (s.CpuWarn, s.CpuCrit);
        (_ramWarn, _ramCrit) = (s.RamWarn, s.RamCrit);
        (_gpuWarn, _gpuCrit) = (s.GpuWarn, s.GpuCrit);
        (_diskWarn, _diskCrit) = (s.DiskWarn, s.DiskCrit);
        _trayShowsCpu = s.TrayShowsCpu;
        _toggleHotkey = s.ToggleHotkey;
        _position = s.Position;
        _customX = s.CustomX;
        _customY = s.CustomY;
        _theme = s.Theme;
        _showTooltip = s.ShowTooltip;
        _clickThrough = s.ClickThrough;
        _selectedGpuAdapterIndex = s.SelectedGpuAdapterIndex;
        _accentColorHex = s.AccentColorHex;
    }

    private void Save()
    {
        // Start from a copy of the current settings so fields this dialog doesn't expose
        // (the resize grip's OverlayScale, the Left/Right position pixel offsets, and
        // anything added later) are preserved rather than silently reset to defaults.
        var settings = _settingsService.Current.Clone();
        settings.StartWithWindows = StartWithWindows;
        settings.StartMinimized = StartMinimized;
        settings.UpdateIntervalMs = UpdateIntervalMs;
        settings.HistorySeconds = HistorySeconds;
        settings.DisplayMode = DisplayMode;
        settings.ShowCpu = ShowCpu;
        settings.ShowRam = ShowRam;
        settings.ShowGpu = ShowGpu;
        settings.ShowNetwork = ShowNetwork;
        settings.ShowDisk = ShowDisk;
        settings.GraphCpu = GraphCpu;
        settings.GraphRam = GraphRam;
        settings.GraphGpu = GraphGpu;
        settings.GraphNetwork = GraphNetwork;
        settings.GraphDisk = GraphDisk;
        settings.NetworkUnit = NetworkUnit;
        (settings.CpuWarn, settings.CpuCrit) = (CpuWarn, CpuCrit);
        (settings.RamWarn, settings.RamCrit) = (RamWarn, RamCrit);
        (settings.GpuWarn, settings.GpuCrit) = (GpuWarn, GpuCrit);
        (settings.DiskWarn, settings.DiskCrit) = (DiskWarn, DiskCrit);
        settings.TrayShowsCpu = TrayShowsCpu;
        settings.ToggleHotkey = ToggleHotkey;
        settings.Position = Position;
        settings.CustomX = CustomX;
        settings.CustomY = CustomY;
        settings.Theme = Theme;
        settings.ShowTooltip = ShowTooltip;
        settings.ClickThrough = ClickThrough;
        settings.SelectedGpuAdapterIndex = SelectedGpuAdapterIndex;
        settings.AccentColorHex = AccentColorHex;

        _startupService.SetEnabled(StartWithWindows);
        _settingsService.Save(settings);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
