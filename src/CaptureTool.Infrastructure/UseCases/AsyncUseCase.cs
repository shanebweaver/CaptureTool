namespace CaptureTool.Infrastructure.UseCases;

using CaptureTool.Infrastructure.Abstractions.UseCases;

public abstract partial class AsyncUseCase : IAsyncUseCase
{
    public virtual bool CanExecute()
    {
        return true;
    }

    public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);
}
