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
        int overlayHeight) =>
        OverlayPlacement.Compute(taskbar, workArea, position, offsetX, offsetY, customX, customY, overlayWidth, overlayHeight);
}
