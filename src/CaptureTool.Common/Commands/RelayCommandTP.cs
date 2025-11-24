using System.Windows.Input;

namespace CaptureTool.Common.Commands;

public sealed partial class RelayCommand<T, P>(Action<T?> commandAction, Func<P?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (canExecute is null)
        {
            return true;
        }

        if (parameter is not P typedParameter)
        {
            return false;
        }

        return CanExecute(typedParameter);
    }

    public bool CanExecute(P parameter)
    {
        if (canExecute is null)
        {
            return true;
        }

        return canExecute.Invoke(parameter);
    }

    public void Execute(object? parameter)
    {
        if (parameter is not T typedParameter)
        {
            return;
        }

        commandAction.Invoke(typedParameter);
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
