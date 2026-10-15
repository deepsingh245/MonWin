namespace SystemMonitor.Services;

/// <summary>Shows the platform's color picker for the accent color.</summary>
public interface IColorPickerService
{
    /// <summary>
    /// Opens the picker (blocking until closed) starting from <paramref name="currentHex"/>
    /// ("#RRGGBB" or null). Returns the chosen "#RRGGBB", or null if the user cancelled.
    /// </summary>
    string? PickColor(string? currentHex);
}
