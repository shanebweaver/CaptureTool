using System.Windows.Input;

namespace CaptureTool.Common.Commands;

public abstract partial class ActionCommandBase : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public abstract bool CanExecute(object? parameter);

    public abstract void Execute(object? parameter);

    protected void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
