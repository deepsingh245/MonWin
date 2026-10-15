using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using SystemMonitor.Services;

namespace MonWin.Linux.Views;

/// <summary>
/// MonWin's colors (the same values as the Windows Light.xaml/Dark.xaml), written into the
/// application resources as "MonWin.*" brushes that the views use via DynamicResource. They are
/// set from code rather than from XAML theme dictionaries so the user's accent override simply
/// replaces two entries, exactly like the Windows app does.
/// </summary>
internal static class ThemePalette
{
    private sealed record Colors(
        string Background, double BackgroundOpacity, string OverlayBackground, double OverlayOpacity,
        string Foreground, string SubtleForeground, string Border, string Accent, double AccentFillOpacity,
        string Hover, string Warning, string Critical);

    private static readonly Colors Light = new(
        "#F3F3F3", 0.92, "#F9F9F9", 0.85, "#1B1B1B", "#5C5C5C", "#D9D9D9", "#0067C0", 0.12, "#E5E5E5", "#9D5D00", "#C42B1C");

    private static readonly Colors Dark = new(
        "#202020", 0.92, "#2C2C2C", 0.85, "#F2F2F2", "#B0B0B0", "#3A3A3A", "#60CDFF", 0.15, "#333333", "#FCE100", "#FF99A4");

    /// <summary>Applies the theme and an optional "#RRGGBB" accent override (invalid values are ignored).</summary>
    public static void Apply(Application app, EffectiveTheme theme, string? accentHex)
    {
        var c = theme == EffectiveTheme.Dark ? Dark : Light;
        app.RequestedThemeVariant = theme == EffectiveTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

        var accent = !string.IsNullOrWhiteSpace(accentHex) && Color.TryParse(accentHex, out var custom)
            ? custom
            : Color.Parse(c.Accent);

        Set(app, "MonWin.Background", new SolidColorBrush(Color.Parse(c.Background), c.BackgroundOpacity));
        Set(app, "MonWin.OverlayBackground", new SolidColorBrush(Color.Parse(c.OverlayBackground), c.OverlayOpacity));
        Set(app, "MonWin.Foreground", new SolidColorBrush(Color.Parse(c.Foreground)));
        Set(app, "MonWin.SubtleForeground", new SolidColorBrush(Color.Parse(c.SubtleForeground)));
        Set(app, "MonWin.Border", new SolidColorBrush(Color.Parse(c.Border)));
        Set(app, "MonWin.Accent", new SolidColorBrush(accent));
        Set(app, "MonWin.AccentFill", new SolidColorBrush(accent, c.AccentFillOpacity));
        Set(app, "MonWin.HoverBackground", new SolidColorBrush(Color.Parse(c.Hover)));
        Set(app, "MonWin.Warning", new SolidColorBrush(Color.Parse(c.Warning)));
        Set(app, "MonWin.Critical", new SolidColorBrush(Color.Parse(c.Critical)));
    }

    private static void Set(Application app, string key, IBrush brush) => app.Resources[key] = brush;
}
