using SystemMonitor.Models;
using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class OverlayPlacementTests
{
    private static readonly ScreenRect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void ComputeFloating_Left_AnchorsToBottomLeft()
    {
        var (x, y) = OverlayPlacement.ComputeFloating(WorkArea, OverlayPosition.Left, 0, 0, 100, 100, 300, 30);

        Assert.Equal(16, x);
        Assert.Equal(1040 - 30 - 16, y);
    }

    [Fact]
    public void ComputeFloating_Center_CentersHorizontally()
    {
        var (x, _) = OverlayPlacement.ComputeFloating(WorkArea, OverlayPosition.Center, 0, 0, 0, 0, 300, 30);

        Assert.Equal((1920 - 300) / 2, x);
    }

    [Fact]
    public void ComputeFloating_Right_AnchorsToBottomRight()
    {
        var (x, _) = OverlayPlacement.ComputeFloating(WorkArea, OverlayPosition.Right, 0, 0, 0, 0, 300, 30);

        Assert.Equal(1920 - 300 - 16, x);
    }

    [Fact]
    public void ComputeFloating_Custom_UsesSavedCoordinates()
    {
        var (x, y) = OverlayPlacement.ComputeFloating(WorkArea, OverlayPosition.Custom, 0, 0, 250, 400, 300, 30);

        Assert.Equal((250, 400), (x, y));
    }

    [Fact]
    public void ComputeFloating_StaleCustomCoordinates_AreKeptOnScreen()
    {
        // A monitor was unplugged since the position was saved.
        var (x, y) = OverlayPlacement.ComputeFloating(WorkArea, OverlayPosition.Custom, 0, 0, 5000, -300, 300, 30);

        Assert.Equal((1920 - 300, 0), (x, y));
    }

    [Fact]
    public void ComputeFloating_OverlayLargerThanWorkArea_DoesNotThrow()
    {
        var tiny = new ScreenRect(0, 0, 100, 50);

        var exception = Record.Exception(() =>
            OverlayPlacement.ComputeFloating(tiny, OverlayPosition.Right, 0, 0, 0, 0, 400, 200));

        Assert.Null(exception);
    }

    [Fact]
    public void Compute_NoTaskbar_FloatsBottomRightRegardlessOfAnchor()
    {
        var none = new TaskbarInfo(false, default, TaskbarEdge.Bottom, false);

        var (x, y) = OverlayPlacement.Compute(none, WorkArea, OverlayPosition.Left, 0, 0, 0, 0, 300, 30);

        Assert.Equal((1920 - 300 - 16, 1040 - 30 - 16), (x, y));
    }
}
