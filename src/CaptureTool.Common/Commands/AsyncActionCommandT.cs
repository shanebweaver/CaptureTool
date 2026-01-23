namespace CaptureTool.Common.Commands;

public abstract partial class AsyncActionCommand<T> : ActionCommandBase, IAsyncActionCommand<T>
{
    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
}