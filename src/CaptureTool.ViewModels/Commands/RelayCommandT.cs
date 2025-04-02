using System;
using System.Windows.Input;

namespace CaptureTool.ViewModels.Commands;

public class RelayCommand<T>(Action<T?> commandAction, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke() ?? true;
    }

    public bool CanExecute()
    {
        return canExecute?.Invoke() ?? true;
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
