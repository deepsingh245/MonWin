using System.Windows.Input;

namespace SystemMonitor.ViewModels.Base;

/// <summary>
/// Framework-neutral <see cref="ICommand"/>. WPF's CommandManager.RequerySuggested is
/// deliberately not used (it would tie shared code to WPF); callers that need to
/// re-evaluate <c>canExecute</c> call <see cref="RaiseCanExecuteChanged"/>.
/// </summary>
public sealed class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }
}
