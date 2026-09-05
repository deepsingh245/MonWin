using Microsoft.Win32;
using SystemMonitor.Models;

namespace SystemMonitor.Services;

/// <summary>
/// Resolves the effective Light/Dark theme, following Windows' own setting when the
/// user has chosen "System". Reads the documented (if informally so) per-user
/// personalization registry value and reacts live via SystemEvents.
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly ILoggingService _logger;

    public event EventHandler? SystemThemeChanged;

    public ThemeService(ILoggingService logger)
    {
        _logger = logger;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public EffectiveTheme Resolve(AppTheme setting) => setting switch
    {
        AppTheme.Light => EffectiveTheme.Light,
        AppTheme.Dark => EffectiveTheme.Dark,
        _ => IsSystemLightTheme() ? EffectiveTheme.Light : EffectiveTheme.Dark,
    };

    private bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i ? i != 0 : true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to read system theme, defaulting to light: {ex.Message}");
            return true;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            SystemThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
