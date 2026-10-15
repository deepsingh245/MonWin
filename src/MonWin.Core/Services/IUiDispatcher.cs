namespace SystemMonitor.Services;

/// <summary>Posts work to the UI thread, so shared view models stay independent of WPF/Avalonia.</summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> on the UI thread without waiting for it.</summary>
    void Post(Action action);
}
