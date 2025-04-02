using System;
using System.Windows.Input;

namespace CaptureTool.ViewModels.Commands;

public class RelayCommand(Action commandAction, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return CanExecute();
    }

    public bool CanExecute()
    {
        return canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        Execute();
    }

    public void Execute()
    {
        commandAction.Invoke();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
