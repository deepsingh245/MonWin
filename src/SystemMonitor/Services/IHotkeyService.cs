namespace SystemMonitor.Services;

public interface IHotkeyService : IDisposable
{
    /// <summary>Human-readable outcome of the last registration attempt, or null when it succeeded or the hotkey is disabled.</summary>
    string? Status { get; }

    /// <summary>Binds to the window that will receive WM_HOTKEY. Call once its handle exists.</summary>
    void Attach(nint hwnd);

    /// <summary>(Re-)registers the given shortcut, replacing any previous one. Empty disables it.</summary>
    void Apply(string? hotkey);

    /// <summary>True when a window message is our hotkey firing.</summary>
    bool IsHotkeyMessage(int msg, nint wParam);
}
