using System.Windows.Input;

namespace CaptureTool.Common.Commands;

public sealed partial class RelayCommand<T>(Action<T?> commandAction, Func<T, bool>? canExecuteFunc = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (canExecuteFunc is null)
        {
            return true;
        }

        if (parameter is not T typedParameter)
        {
            return false;
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
        if (parameter is not T typedParameter)
        {
            return;
        }

        Execute(typedParameter);
    }

    public void Execute(T parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        commandAction.Invoke(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}