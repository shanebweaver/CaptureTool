namespace CaptureTool.Common.Commands;

public abstract partial class AsyncActionCommand<T> : ActionCommandBase, IAsyncActionCommand<T>
{
    public override bool CanExecute(object? parameter)
    {
        throw new NotImplementedException();
    }

    public override void Execute(object? parameter)
    {
        throw new NotImplementedException();
    }

    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
}