namespace CaptureTool.Common.Commands;

public abstract partial class AsyncActionCommand : ActionCommandBase, IAsyncActionCommand
{
    public override bool CanExecute(object? _)
    {
        throw new NotImplementedException();
    }

    public override void Execute(object? _)
    {
        throw new NotImplementedException();
    }

    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);
}