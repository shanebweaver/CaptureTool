namespace CaptureTool.Infrastructure.UseCases;

using CaptureTool.Infrastructure.Abstractions.UseCases;

public abstract partial class AsyncUseCase<T> : IAsyncUseCase<T>
{
    public virtual bool CanExecute(T parameter)
    {
        return true;
    }

    public abstract Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
}