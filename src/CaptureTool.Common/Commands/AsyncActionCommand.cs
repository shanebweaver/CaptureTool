namespace CaptureTool.Common.Commands;

public abstract partial class AsyncActionCommand : ActionCommandBase, IAsyncActionCommand
{
    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);
}