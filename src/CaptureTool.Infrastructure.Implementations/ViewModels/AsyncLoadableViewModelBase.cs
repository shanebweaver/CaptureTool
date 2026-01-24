using CaptureTool.Infrastructure.Interfaces.Loading;

namespace CaptureTool.Infrastructure.Implementations.ViewModels;

public abstract partial class AsyncLoadableViewModelBase : ViewModelBase, IAsyncLoadable
{
    public virtual Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}
