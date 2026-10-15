namespace MonWin.Linux.Desktop;

internal enum SessionKind
{
    X11,
    Wayland,
    Unknown,
}

/// <summary>
/// What kind of graphical session MonWin was started in. Linux desktops differ a lot: there
/// is no universal taskbar to embed into, and Wayland deliberately forbids apps from
/// positioning or stacking their own windows globally. This is only used to explain the
/// limits (log and docs); no code path tries to work around a compositor's restrictions.
/// </summary>
internal sealed record DesktopSession(SessionKind Kind, string DesktopEnvironment)
{
    public static DesktopSession Detect(Func<string, string?> env)
    {
        var sessionType = env("XDG_SESSION_TYPE")?.Trim().ToLowerInvariant();
        var kind = sessionType switch
        {
            "wayland" => SessionKind.Wayland,
            "x11" => SessionKind.X11,
            _ => !string.IsNullOrEmpty(env("WAYLAND_DISPLAY")) ? SessionKind.Wayland
                : !string.IsNullOrEmpty(env("DISPLAY")) ? SessionKind.X11
                : SessionKind.Unknown,
        };

        return new DesktopSession(kind, env("XDG_CURRENT_DESKTOP")?.Trim() ?? string.Empty);
    }

    public static DesktopSession FromEnvironment() => Detect(Environment.GetEnvironmentVariable);

    /// <summary>One line for the log describing what MonWin can and cannot do here.</summary>
    public string Describe() => Kind switch
    {
        SessionKind.X11 => $"X11 session ({Label}): overlay position and always-on-top are controlled by the window manager.",
        SessionKind.Wayland => $"Wayland session ({Label}): MonWin runs through XWayland; the compositor may ignore requested position or always-on-top.",
        _ => $"No graphical session detected ({Label}).",
    };

    private string Label => string.IsNullOrEmpty(DesktopEnvironment) ? "unknown desktop" : DesktopEnvironment;
}
