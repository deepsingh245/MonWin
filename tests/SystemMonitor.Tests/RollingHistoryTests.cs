using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class RollingHistoryTests
{
    [Fact]
    public void Add_WithinCapacity_KeepsAllValuesInOrder()
    {
        var history = new RollingHistory(5);
        history.Add(1);
        history.Add(2);
        history.Add(3);

        Assert.Equal([1.0, 2.0, 3.0], history.ToArray());
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void Add_BeyondCapacity_DropsOldest()
    {
        var history = new RollingHistory(3);
        history.Add(1);
        history.Add(2);
        history.Add(3);
        history.Add(4);

        Assert.Equal([2.0, 3.0, 4.0], history.ToArray());
        Assert.Equal(3, history.Count);
        Assert.Equal(3, history.Capacity);
    }

    [Fact]
    public void Resize_Larger_PreservesExistingSamples()
    {
        var history = new RollingHistory(2);
        history.Add(1);
        history.Add(2);

        history.Resize(5);
        history.Add(3);

        Assert.Equal([1.0, 2.0, 3.0], history.ToArray());
        Assert.Equal(5, history.Capacity);
    }

    [Fact]
    public void Resize_Smaller_KeepsMostRecentSamples()
    {
        var history = new RollingHistory(5);
        history.Add(1);
        history.Add(2);
        history.Add(3);
        history.Add(4);

        history.Resize(2);

        Assert.Equal([3.0, 4.0], history.ToArray());
        Assert.Equal(2, history.Capacity);
    }

    [Fact]
    public void Constructor_InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingHistory(0));
    }
}
