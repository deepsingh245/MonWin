using SystemMonitor.Services;

namespace SystemMonitor.Tests;

internal sealed class NoOpLoggingService : ILoggingService
{
    public void LogInfo(string message) { }
    public void LogWarning(string message) { }
    public void LogError(string message, Exception? exception = null) { }
}
