using CaptureTool.Infrastructure.Interfaces.Loading;

namespace CaptureTool.Infrastructure.Implementations.Loading;

public abstract partial class AsyncLoadable : HasLoadStateBase, IAsyncLoadable
{
    public virtual Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}
