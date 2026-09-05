using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

/// <summary>
/// Exercises the real HKCU Run key (no admin required for HKCU) using the service's
/// actual value name, cleaning up afterwards so the dev machine isn't left with a
/// dangling startup entry.
/// </summary>
public class StartupServiceTests : IDisposable
{
    private readonly StartupService _service = new(new NoOpLoggingService());

    [Fact]
    public void SetEnabled_True_ThenIsEnabled_ReturnsTrue()
    {
        _service.SetEnabled(true);
        Assert.True(_service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_False_ThenIsEnabled_ReturnsFalse()
    {
        _service.SetEnabled(true);
        _service.SetEnabled(false);
        Assert.False(_service.IsEnabled());
    }

    public void Dispose()
    {
        _service.SetEnabled(false);
    }
}
