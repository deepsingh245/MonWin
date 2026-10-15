using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface ITaskbarService
{
    /// <summary>Locates the primary taskbar. Never throws — returns Found=false on any failure.</summary>
    TaskbarInfo GetTaskbarInfo();

    /// <summary>
    /// Computes the top-left screen position for an overlay window of the given size,
    /// honoring the configured <see cref="OverlayPosition"/>. Falls back to a sensible
    /// floating position when the taskbar cannot be located.
    /// </summary>
    (int X, int Y) ComputeOverlayPosition(
        TaskbarInfo taskbar,
        ScreenRect workArea,
        OverlayPosition position,
        int offsetX,
        int offsetY,
        int customX,
        int customY,
        int overlayWidth,
        int overlayHeight);
}
