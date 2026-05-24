namespace CaptureTool.Infrastructure.UseCases;

using CaptureTool.Infrastructure.Abstractions.UseCases;

public abstract partial class UseCase<T> : IUseCase<T>
{
    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract void Execute(T parameter);
}