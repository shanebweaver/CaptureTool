namespace CaptureTool.Infrastructure.Implementations.UseCases;

using CaptureTool.Infrastructure.Interfaces.UseCases;

public abstract partial class UseCase : IUseCase
{
    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract void Execute();
}
