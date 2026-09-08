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

    public event EventHandler? RequestClose;

    public SettingsViewModel(ISettingsService settingsService, IStartupService startupService, IGpuMonitor gpuMonitor)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _gpuMonitor = gpuMonitor;

        LoadFrom(_settingsService.Current);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
        SelectAccentColorCommand = new RelayCommand(p => AccentColorHex = p as string);
        PickCustomColorCommand = new RelayCommand(PickCustomColor);
        ResetAccentColorCommand = new RelayCommand(() => AccentColorHex = null);
    }

    private void PickCustomColor()
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (!string.IsNullOrWhiteSpace(AccentColorHex) && TryParseHex(AccentColorHex, out var r, out var g, out var b))
        {
            dialog.Color = System.Drawing.Color.FromArgb(r, g, b);
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dialog.Color;
            AccentColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private static bool TryParseHex(string hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var clean = hex.TrimStart('#');
        if (clean.Length != 6)
        {
            return false;
        }

        return byte.TryParse(clean[..2], System.Globalization.NumberStyles.HexNumber, null, out r)
            && byte.TryParse(clean[2..4], System.Globalization.NumberStyles.HexNumber, null, out g)
            && byte.TryParse(clean[4..6], System.Globalization.NumberStyles.HexNumber, null, out b);
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
        _accentColorHex = s.AccentColorHex;
    }

    private void Save()
    {
        // Start from the current settings so fields this dialog doesn't expose (the
        // resize grip's OverlayScale, and the Left/Right position pixel offsets) are
        // preserved rather than silently reset to their defaults.
        var current = _settingsService.Current;
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
            PositionOffsetX = current.PositionOffsetX,
            PositionOffsetY = current.PositionOffsetY,
            CustomX = CustomX,
            CustomY = CustomY,
            Theme = Theme,
            ShowTooltip = ShowTooltip,
            ClickThrough = ClickThrough,
            SelectedGpuAdapterIndex = SelectedGpuAdapterIndex,
            AccentColorHex = AccentColorHex,
            OverlayScale = current.OverlayScale,
        };

        _startupService.SetEnabled(StartWithWindows);
        _settingsService.Save(settings);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
