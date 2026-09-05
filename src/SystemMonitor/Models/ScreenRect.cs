namespace SystemMonitor.Models;

/// <summary>Plain screen-coordinate rectangle, kept independent of any UI framework type
/// so the taskbar-geometry logic in Services stays unit-testable without WPF.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public enum TaskbarEdge
{
    Left,
    Top,
    Right,
    Bottom,
}

public sealed record TaskbarInfo(bool Found, ScreenRect Bounds, TaskbarEdge Edge, bool IsAutoHide);
