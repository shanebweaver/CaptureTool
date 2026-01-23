using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Infrastructure.Implementations.Commands;

/// <summary>
/// Implementation of <see cref="IAppCommand"/> that executes a delegate.
/// </summary>
public sealed class AppCommand : IAppCommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public AppCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public void Execute()
    {
        _execute();
    }

    public bool CanExecute()
    {
        return _canExecute?.Invoke() ?? true;
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
