namespace CaptureTool.Infrastructure.Implementations.UseCases;

using CaptureTool.Infrastructure.Interfaces.UseCases;

public abstract partial class AsyncUseCase<T> : IAsyncUseCase<T>
{
    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
}