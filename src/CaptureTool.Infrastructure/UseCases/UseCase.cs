namespace CaptureTool.Infrastructure.UseCases;

using CaptureTool.Infrastructure.Abstractions.UseCases;

public abstract partial class UseCase : IUseCase
{
    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract void Execute();
}
