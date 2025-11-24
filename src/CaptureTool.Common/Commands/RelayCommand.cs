using System.Windows.Input;

namespace CaptureTool.Common.Commands;

public sealed partial class RelayCommand(Action commandAction, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return CanExecute();
    }

    public bool CanExecute()
    {
        if (canExecute is null)
        {
            return true;
        }

        return canExecute.Invoke();
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute())
        {
            return;
        }

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
