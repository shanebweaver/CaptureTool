using CaptureTool.Infrastructure.Abstractions.Loading;

namespace CaptureTool.Infrastructure.Loading;

public abstract partial class AsyncLoadable : HasLoadStateBase, IAsyncLoadable
{
    public virtual Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}
