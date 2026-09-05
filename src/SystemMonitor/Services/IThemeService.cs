using SystemMonitor.Models;

namespace SystemMonitor.Services;

public enum EffectiveTheme
{
    Light,
    Dark,
}

public interface IThemeService
{
    EffectiveTheme Resolve(AppTheme setting);
    event EventHandler? SystemThemeChanged;
}
