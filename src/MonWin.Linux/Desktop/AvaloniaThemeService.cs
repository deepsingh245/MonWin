using Avalonia;
using Avalonia.Platform;
using SystemMonitor.Models;
using SystemMonitor.Services;

namespace MonWin.Linux.Desktop;

/// <summary>Resolves Light/Dark, following the desktop's own preference (via Avalonia's platform settings) when the user chose "System".</summary>
internal sealed class AvaloniaThemeService : IThemeService, IDisposable
{
    private IPlatformSettings? _settings;

    public event EventHandler? SystemThemeChanged;

    public EffectiveTheme Resolve(AppTheme setting)
    {
        switch (setting)
        {
            case AppTheme.Light:
                return EffectiveTheme.Light;
            case AppTheme.Dark:
                return EffectiveTheme.Dark;
        }

        Subscribe();
        // Desktops that expose no preference are treated as light (matches the Windows service).
        return _settings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark ? EffectiveTheme.Dark : EffectiveTheme.Light;
    }

    private void Subscribe()
    {
        if (_settings is not null)
        {
            return;
        }

        _settings = Application.Current?.PlatformSettings;
        if (_settings is not null)
        {
            _settings.ColorValuesChanged += OnColorValuesChanged;
        }
    }

    private void OnColorValuesChanged(object? sender, PlatformColorValues e) => SystemThemeChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_settings is not null)
        {
            _settings.ColorValuesChanged -= OnColorValuesChanged;
        }
    }
}
