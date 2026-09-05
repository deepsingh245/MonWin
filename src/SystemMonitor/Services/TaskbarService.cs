using SystemMonitor.Models;
using SystemMonitor.Native;

namespace SystemMonitor.Services;

/// <summary>
/// Locates the Windows taskbar via the documented Shell_TrayWnd + SHAppBarMessage
/// mechanism (no Explorer hooking — we only read geometry of a window that already
/// exists) and computes where a small overlay window should sit relative to it.
/// </summary>
public sealed class TaskbarService : ITaskbarService
{
    private const int StartButtonAvoidanceWidth = 64;
    private const int SystemTrayAvoidanceWidth = 160;
    private const int GapAboveTaskbar = 6;

    private readonly ILoggingService _logger;

    public TaskbarService(ILoggingService logger)
    {
        _logger = logger;
    }

    public TaskbarInfo GetTaskbarInfo()
    {
        try
        {
            var hwnd = TaskbarInterop.FindWindow("Shell_TrayWnd", null);
            if (hwnd == 0)
            {
                _logger.LogWarning("Shell_TrayWnd not found; falling back to floating overlay mode.");
                return new TaskbarInfo(false, default, TaskbarEdge.Bottom, false);
            }

            var appBarData = new TaskbarInterop.AppBarData
            {
                CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<TaskbarInterop.AppBarData>(),
                HWnd = hwnd,
            };

            TaskbarInterop.SHAppBarMessage(TaskbarInterop.ABM_GETTASKBARPOS, ref appBarData);
            var state = TaskbarInterop.SHAppBarMessage(TaskbarInterop.ABM_GETSTATE, ref appBarData);
            var isAutoHide = ((int)state & TaskbarInterop.ABS_AUTOHIDE) != 0;

            var edge = appBarData.UEdge switch
            {
                TaskbarInterop.ABE_LEFT => TaskbarEdge.Left,
                TaskbarInterop.ABE_TOP => TaskbarEdge.Top,
                TaskbarInterop.ABE_RIGHT => TaskbarEdge.Right,
                _ => TaskbarEdge.Bottom,
            };

            var bounds = new ScreenRect(appBarData.Rc.Left, appBarData.Rc.Top, appBarData.Rc.Right, appBarData.Rc.Bottom);
            return new TaskbarInfo(true, bounds, edge, isAutoHide);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Taskbar detection failed: {ex.Message}");
            return new TaskbarInfo(false, default, TaskbarEdge.Bottom, false);
        }
    }

    public (int X, int Y) ComputeOverlayPosition(
        TaskbarInfo taskbar,
        ScreenRect workArea,
        OverlayPosition position,
        int offsetX,
        int offsetY,
        int customX,
        int customY,
        int overlayWidth,
        int overlayHeight)
    {
        if (position == OverlayPosition.Custom)
        {
            return (customX + offsetX, customY + offsetY);
        }

        if (!taskbar.Found)
        {
            // Floating fallback: bottom-right corner of the work area (never covers the
            // real taskbar since Windows already excludes it from the work area).
            var fx = workArea.Right - overlayWidth - 16 + offsetX;
            var fy = workArea.Bottom - overlayHeight - 16 + offsetY;
            return (fx, fy);
        }

        // v1 focuses on the standard bottom taskbar (Windows 11 default and only
        // user-selectable position); other edges use the same anchoring logic against
        // the taskbar's own bounds, documented as best-effort in TROUBLESHOOTING.md.
        var y = taskbar.Bounds.Top - overlayHeight - GapAboveTaskbar + offsetY;
        if (y < workArea.Top)
        {
            y = taskbar.Bounds.Top + offsetY;
        }

        var x = position switch
        {
            OverlayPosition.Left => taskbar.Bounds.Left + StartButtonAvoidanceWidth + offsetX,
            OverlayPosition.Right => taskbar.Bounds.Right - overlayWidth - SystemTrayAvoidanceWidth + offsetX,
            _ => taskbar.Bounds.Left + (taskbar.Bounds.Width - overlayWidth) / 2 + offsetX,
        };

        return (x, y);
    }
}
