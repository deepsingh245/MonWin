using SystemMonitor.Models;

namespace SystemMonitor.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save(AppSettings settings);
    event EventHandler<AppSettings>? SettingsChanged;
}
