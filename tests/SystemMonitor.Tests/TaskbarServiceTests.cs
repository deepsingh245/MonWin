using SystemMonitor.Models;
using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class TaskbarServiceTests
{
    private readonly TaskbarService _service = new(new NoOpLoggingService());

    // A typical 1920x1080 screen with a 48px-tall bottom taskbar.
    private static readonly TaskbarInfo BottomTaskbar = new(
        Found: true,
        Bounds: new ScreenRect(0, 1032, 1920, 1080),
        Edge: TaskbarEdge.Bottom,
        IsAutoHide: false);

    private static readonly ScreenRect WorkArea = new(0, 0, 1920, 1032);

    [Fact]
    public void ComputeOverlayPosition_OverlayFitsInTaskbar_CentersVerticallyWithinIt()
    {
        var (_, y) = _service.ComputeOverlayPosition(
            BottomTaskbar, WorkArea, OverlayPosition.Left,
            offsetX: 0, offsetY: 0, customX: 0, customY: 0,
            overlayWidth: 300, overlayHeight: 32);

        // Taskbar spans 1032-1080 (height 48); a 32px-tall overlay centered within it
        // sits at 1032 + (48-32)/2 = 1040.
        Assert.Equal(1040, y);
    }

    [Fact]
    public void ComputeOverlayPosition_OverlayTallerThanTaskbar_FloatsAboveInstead()
    {
        var (_, y) = _service.ComputeOverlayPosition(
            BottomTaskbar, WorkArea, OverlayPosition.Left,
            offsetX: 0, offsetY: 0, customX: 0, customY: 0,
            overlayWidth: 300, overlayHeight: 400);

        // Taller than the 48px taskbar: must sit above it, not overlap/spill past both edges.
        Assert.True(y < BottomTaskbar.Bounds.Top);
    }

    [Fact]
    public void ComputeOverlayPosition_Left_AnchorsNearLeftEdgeOfTaskbar()
    {
        var (x, _) = _service.ComputeOverlayPosition(
            BottomTaskbar, WorkArea, OverlayPosition.Left,
            offsetX: 0, offsetY: 0, customX: 0, customY: 0,
            overlayWidth: 300, overlayHeight: 32);

        Assert.True(x > BottomTaskbar.Bounds.Left);
        Assert.True(x < BottomTaskbar.Bounds.Left + 200);
    }

    [Fact]
    public void ComputeOverlayPosition_Right_AnchorsNearRightEdgeOfTaskbar()
    {
        var (x, _) = _service.ComputeOverlayPosition(
            BottomTaskbar, WorkArea, OverlayPosition.Right,
            offsetX: 0, offsetY: 0, customX: 0, customY: 0,
            overlayWidth: 300, overlayHeight: 32);

        Assert.True(x + 300 <= BottomTaskbar.Bounds.Right);
    }

    [Fact]
    public void ComputeOverlayPosition_Custom_IgnoresTaskbarEntirely()
    {
        var (x, y) = _service.ComputeOverlayPosition(
            BottomTaskbar, WorkArea, OverlayPosition.Custom,
            offsetX: 5, offsetY: 7, customX: 100, customY: 200,
            overlayWidth: 300, overlayHeight: 32);

        Assert.Equal(105, x);
        Assert.Equal(207, y);
    }

    [Fact]
    public void ComputeOverlayPosition_TaskbarNotFound_FallsBackToBottomRightOfWorkArea()
    {
        var notFound = new TaskbarInfo(false, default, TaskbarEdge.Bottom, false);

        var (x, y) = _service.ComputeOverlayPosition(
            notFound, WorkArea, OverlayPosition.Left,
            offsetX: 0, offsetY: 0, customX: 0, customY: 0,
            overlayWidth: 300, overlayHeight: 32);

        Assert.True(x < WorkArea.Right);
        Assert.True(y < WorkArea.Bottom);
    }
}
