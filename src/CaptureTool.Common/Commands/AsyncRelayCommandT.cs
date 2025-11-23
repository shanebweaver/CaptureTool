using System.Windows.Input;

namespace CaptureTool.Common.Commands;

public sealed class AsyncRelayCommand<T>(Func<T, Task> executeAsync, Func<T, bool>? canExecuteFunc = null, bool isExecuting = false) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (canExecuteFunc == null)
        {
            return !isExecuting;
        }

        if (isExecuting)
        {
            return false;
        }

        if (parameter is not T typedParameter)
        {
            throw new InvalidOperationException("Invalid command parameter type.");
        }

        return CanExecute(typedParameter);
    }

    public bool CanExecute(T parameter)
    {
        if (canExecuteFunc is null)
        {
            return true;
        }

        return canExecuteFunc.Invoke(parameter);
    }

    public void Execute(object? parameter)
    {
        throw new InvalidOperationException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }

    public async Task ExecuteAsync(T parameter)
    {
        try
        {
            isExecuting = true;
            RaiseCanExecuteChanged();
            await executeAsync(parameter);
        }
        finally
        {
            isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}