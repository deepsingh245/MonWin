using SystemMonitor.Models;

namespace SystemMonitor.Services;

/// <summary>
/// Pure overlay-positioning math, shared by every platform's <see cref="ITaskbarService"/>.
/// Kept free of any OS or UI-framework type so it is unit-testable everywhere.
/// </summary>
public static class OverlayPlacement
{
    private const int StartButtonAvoidanceWidth = 64;
    private const int SystemTrayAvoidanceWidth = 160;
    private const int GapAboveTaskbar = 6;
    private const int FloatingMargin = 16;

    /// <summary>
    /// Windows behavior: sit on the taskbar strip when one was found, otherwise float in
    /// the bottom-right corner of the work area. See TaskbarService for the rationale.
    /// </summary>
    public static (int X, int Y) Compute(
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
            var fx = workArea.Right - overlayWidth - FloatingMargin + offsetX;
            var fy = workArea.Bottom - overlayHeight - FloatingMargin + offsetY;
            return (fx, fy);
        }

        // v1 focuses on the standard bottom taskbar (Windows 11 default and only
        // user-selectable position); other edges use the same anchoring logic against
        // the taskbar's own bounds, documented as best-effort in TROUBLESHOOTING.md.
        //
        // Sit directly on the taskbar strip (vertically centered within it) whenever the
        // overlay is small enough to fit — this is what makes it read as "part of the
        // taskbar" rather than a separate floating bar. Only fall back to floating just
        // above it if the overlay (e.g. scaled up via the resize grip) is taller than the
        // taskbar itself, since centering it would then spill off both edges.
        var y = overlayHeight <= taskbar.Bounds.Height
            ? taskbar.Bounds.Top + (taskbar.Bounds.Height - overlayHeight) / 2 + offsetY
            : taskbar.Bounds.Top - overlayHeight - GapAboveTaskbar + offsetY;

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

    /// <summary>
    /// Desktops without a queryable taskbar (Linux): the overlay floats along the bottom
    /// edge of the work area — which already excludes panels the desktop reserved — anchored
    /// Left/Center/Right. Custom uses the saved coordinates. The result is always kept inside
    /// the work area so a stale offset can't push the overlay off-screen.
    /// </summary>
    public static (int X, int Y) ComputeFloating(
        ScreenRect workArea,
        OverlayPosition position,
        int offsetX,
        int offsetY,
        int customX,
        int customY,
        int overlayWidth,
        int overlayHeight)
    {
        int x;
        int y;
        if (position == OverlayPosition.Custom)
        {
            x = customX + offsetX;
            y = customY + offsetY;
        }
        else
        {
            x = position switch
            {
                OverlayPosition.Left => workArea.Left + FloatingMargin,
                OverlayPosition.Right => workArea.Right - overlayWidth - FloatingMargin,
                _ => workArea.Left + (workArea.Width - overlayWidth) / 2,
            } + offsetX;
            y = workArea.Bottom - overlayHeight - FloatingMargin + offsetY;
        }

        // Clamp only when the overlay fits; an overlay bigger than the work area is left
        // alone rather than producing an inverted range.
        if (workArea.Width >= overlayWidth)
        {
            x = Math.Clamp(x, workArea.Left, workArea.Right - overlayWidth);
        }

        if (workArea.Height >= overlayHeight)
        {
            y = Math.Clamp(y, workArea.Top, workArea.Bottom - overlayHeight);
        }

        return (x, y);
    }
}
