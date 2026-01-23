namespace CaptureTool.Common.Commands;

public abstract partial class ActionCommand : ActionCommandBase, IActionCommand
{
    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract void Execute();
}
