namespace CaptureTool.Common.Commands;

public abstract partial class ActionCommand<T> : ActionCommandBase, IActionCommand<T>
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

        Execute(typedParameter);
    }

    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract void Execute(T parameter);
}