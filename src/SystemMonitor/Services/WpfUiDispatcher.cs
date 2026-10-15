using System.Windows.Threading;

namespace SystemMonitor.Services;

/// <summary>Adapts the WPF <see cref="Dispatcher"/> to the framework-neutral <see cref="IUiDispatcher"/>.</summary>
public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action) => dispatcher.BeginInvoke(action);
}
