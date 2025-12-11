namespace CaptureTool.Common.Commands;

public abstract partial class AsyncActionCommand<T> : ActionCommandBase, IAsyncActionCommand<T>
{
    public override bool CanExecute(object? parameter)
    {
        if (parameter is not T typedParameter)
        {
            throw new ArgumentException("Unexpected parameter type.");
        }

        return CanExecute(typedParameter);
    }

    public override void Execute(object? parameter)
    {
        if (parameter is not T typedParameter)
        {
            throw new ArgumentException("Unexpected parameter type.");
        }

        ExecuteAsync(typedParameter);
    }

    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract Task ExecuteAsync(T parameter);
}