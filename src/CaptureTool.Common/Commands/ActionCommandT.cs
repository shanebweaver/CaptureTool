namespace CaptureTool.Common.Commands;

public abstract partial class ActionCommand<T> : ActionCommandBase, IActionCommand<T>
{
    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract void Execute(T parameter);
}