using System;
using System.Windows.Input;

namespace CaptureTool.ViewModels.Commands;

public class RelayCommand<T, P>(Action<T?> commandAction, Func<P?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke((P?)parameter) ?? true;
    }

    public bool CanExecute(P? parameter)
    {
        return canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        commandAction.Invoke((T?)parameter);
    }

    public void Execute(T? parameter)
    {
        commandAction.Invoke(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
