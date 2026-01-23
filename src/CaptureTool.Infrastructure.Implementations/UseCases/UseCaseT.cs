namespace CaptureTool.Infrastructure.Implementations.UseCases;

using CaptureTool.Infrastructure.Interfaces.UseCases;

public abstract partial class UseCase<T> : IUseCase<T>
{
    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract void Execute(T parameter);
}