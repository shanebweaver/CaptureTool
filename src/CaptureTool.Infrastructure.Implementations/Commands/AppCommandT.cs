using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Infrastructure.Implementations.Commands;

/// <summary>
/// Implementation of <see cref="IAppCommand{T}"/> that executes a delegate with a parameter.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public sealed class AppCommand<T> : IAppCommand<T>
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

    public AppCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public void Execute(T? parameter)
    {
        _execute(parameter);
    }

    public bool CanExecute(T? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
