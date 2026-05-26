using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Infrastructure.Commands;

/// <summary>
/// Implementation of <see cref="IAppCommand"/> that executes a delegate.
/// </summary>
public class AppCommand : IAppCommand
{
    public static AppCommand Create(Action execute)
    {
        return new AppCommand(execute);
    }

    public static AppCommand Create(Action execute, Func<bool> canExecute)
    {
        return new AppCommand(execute, canExecute);
    }

    protected AppCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

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
