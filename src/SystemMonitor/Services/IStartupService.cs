namespace SystemMonitor.Services;

public interface IStartupService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}
