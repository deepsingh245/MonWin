using SystemMonitor.Models;
using SystemMonitor.Services;

namespace MonWin.Linux.Desktop;

/// <summary>
/// Linux has no single taskbar to dock to: GNOME, KDE, Xfce and tiling window managers each
/// draw their own panel, and Wayland gives apps no supported way to embed into one. So the
/// overlay floats along an edge of the work area (which already excludes any panel the
/// desktop reserved) — "panel-adjacent" rather than "in the panel".
/// </summary>
internal sealed class LinuxPanelService : ITaskbarService
{
    public TaskbarInfo GetTaskbarInfo() => new(Found: false, default, TaskbarEdge.Bottom, IsAutoHide: false);

    public (int X, int Y) ComputeOverlayPosition(
        TaskbarInfo taskbar,
        ScreenRect workArea,
        OverlayPosition position,
        int offsetX,
        int offsetY,
        int customX,
        int customY,
        int overlayWidth,
        int overlayHeight) =>
        OverlayPlacement.ComputeFloating(workArea, position, offsetX, offsetY, customX, customY, overlayWidth, overlayHeight);
}
