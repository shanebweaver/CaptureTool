namespace CaptureTool.Common.Commands;

public abstract partial class AsyncActionCommand : ActionCommandBase, IAsyncActionCommand
{
    public override bool CanExecute(object? _)
    {
        return CanExecute();
    }

    public override void Execute(object? _)
    {
        ExecuteAsync();
    }

    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract Task ExecuteAsync();
}