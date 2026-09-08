using SystemMonitor.Models;
using Xunit;

namespace SystemMonitor.Tests;

public class AppSettingsTests
{
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, AppSettings.MinOverlayScale)]
    [InlineData(3.0, AppSettings.MaxOverlayScale)]
    [InlineData(AppSettings.MinOverlayScale, AppSettings.MinOverlayScale)]
    [InlineData(AppSettings.MaxOverlayScale, AppSettings.MaxOverlayScale)]
    public void ClampOverlayScale_ClampsToValidRange(double input, double expected)
    {
        Assert.Equal(expected, AppSettings.ClampOverlayScale(input));
    }

    [Fact]
    public void Defaults_OverlayScaleIsOne_AccentColorIsNull()
    {
        var settings = new AppSettings();
        Assert.Equal(1.0, settings.OverlayScale);
        Assert.Null(settings.AccentColorHex);
    }
}
