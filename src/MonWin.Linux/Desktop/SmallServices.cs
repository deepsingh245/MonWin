using Avalonia.Threading;
using SystemMonitor.Services;

namespace MonWin.Linux.Desktop;

/// <summary>Global shortcuts aren't offered on Linux: X11 would need a key grab and Wayland forbids them for ordinary apps.</summary>
internal sealed class UnsupportedHotkeyService : IHotkeyService
{
    public string? Status => "Global shortcuts are not supported on Linux yet.";

    public void Attach(nint hwnd)
    {
    }

    public void Apply(string? hotkey)
    {
    }

    public bool IsHotkeyMessage(int msg, nint wParam) => false;

    public void Dispose()
    {
    }
}

/// <summary>Accent colors on Linux are chosen from presets or typed as #RRGGBB; there is no native picker dialog.</summary>
internal sealed class NoColorPickerService : IColorPickerService
{
    public string? PickColor(string? currentHex) => null;
}

internal sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
