namespace CaptureTool.Common.Commands;

public abstract partial class ActionCommand : ActionCommandBase
{
    public override bool CanExecute(object? _)
    {
        return CanExecute();
    }

    public override void Execute(object? _)
    {
        Execute();
    }

    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract void Execute();
}
