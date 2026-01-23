namespace CaptureTool.Infrastructure.Implementations.UseCases;

using CaptureTool.Infrastructure.Interfaces.UseCases;

public abstract partial class AsyncUseCase : IAsyncUseCase
{
    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);
}
