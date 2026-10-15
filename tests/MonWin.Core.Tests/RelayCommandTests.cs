using SystemMonitor.ViewModels.Base;
using Xunit;

namespace SystemMonitor.Tests;

public class RelayCommandTests
{
    [Fact]
    public void Execute_InvokesAction()
    {
        var calls = 0;
        var command = new RelayCommand(() => calls++);

        command.Execute(null);

        Assert.Equal(1, calls);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RaiseCanExecuteChanged_NotifiesSubscribers()
    {
        var allowed = false;
        var command = new RelayCommand(() => { }, () => allowed);
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        allowed = true;
        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
        Assert.True(command.CanExecute(null));
    }
}
