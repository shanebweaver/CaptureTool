using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Infrastructure.Commands;

/// <summary>
/// Implementation of <see cref="IAppCommand{T}"/> that executes a delegate with a parameter.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public class SimpleAppCommand<T> : IAppCommand<T> where T : notnull
{
    public static SimpleAppCommand<T> Create(Action<T?> execute)
    {
        return new SimpleAppCommand<T>(execute);
    }

    public static SimpleAppCommand<T> Create(Action<T?> execute, Predicate<T?>? canExecute)
    {
        return new SimpleAppCommand<T>(execute, canExecute);
    }

    protected SimpleAppCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

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
