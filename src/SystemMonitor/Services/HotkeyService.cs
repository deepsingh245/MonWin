using System.Runtime.InteropServices;
using SystemMonitor.Native;

namespace SystemMonitor.Services;

/// <summary>
/// Owns the single global show/hide hotkey via RegisterHotKey — a documented, per-user
/// API that needs no keyboard hook. Registration fails (rather than stealing the key) when
/// another app already owns the combination; that is surfaced via <see cref="Status"/>.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int HotkeyId = 0x4D57; // "MW"

    private readonly ILoggingService _logger;
    private nint _hwnd;
    private bool _registered;
    private string? _pending;

    public string? Status { get; private set; }

    public HotkeyService(ILoggingService logger)
    {
        _logger = logger;
    }

    public void Attach(nint hwnd)
    {
        _hwnd = hwnd;
        if (_pending is not null)
        {
            Apply(_pending);
        }
    }

    public void Apply(string? hotkey)
    {
        _pending = hotkey ?? string.Empty;
        if (_hwnd == 0)
        {
            return;
        }

        Unregister();

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            Status = null;
            return;
        }

        if (!HotkeyParser.TryParse(hotkey, out var modifiers, out var vk))
        {
            Status = $"\"{hotkey}\" is not a valid shortcut (e.g. Ctrl+Alt+M).";
            _logger.LogWarning($"Invalid hotkey setting: {hotkey}");
            return;
        }

        if (NativeMethods.RegisterHotKey(_hwnd, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, vk))
        {
            _registered = true;
            Status = null;
        }
        else
        {
            Status = $"{hotkey} is already used by another app.";
            _logger.LogWarning($"RegisterHotKey failed for {hotkey} (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    public bool IsHotkeyMessage(int msg, nint wParam) =>
        _registered && msg == NativeMethods.WM_HOTKEY && wParam == HotkeyId;

    private void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose() => Unregister();
}
