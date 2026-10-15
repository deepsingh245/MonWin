using System.Reflection;
using SystemMonitor.ViewModels.Base;

namespace SystemMonitor.ViewModels;

public sealed class AboutViewModel : ObservableObject
{
    // The entry assembly is the running app (Windows or Linux); this shared assembly's
    // own version would be the same only by coincidence.
    public string Version { get; } =
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString(3) ?? "1.0.0";

    public string PrivacyStatement { get; } =
        "MonWin runs entirely on this computer. It makes no network connections, collects no " +
        "telemetry or analytics, and does not use a kernel driver or any elevated privileges. " +
        "See docs/SECURITY.md and docs/PRIVACY notes in the project README for details.";
}
