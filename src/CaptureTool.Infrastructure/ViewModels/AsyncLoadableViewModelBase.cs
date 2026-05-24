using CaptureTool.Infrastructure.Abstractions.Loading;

namespace CaptureTool.Infrastructure.ViewModels;

public abstract partial class AsyncLoadableViewModelBase : ViewModelBase, IAsyncLoadable
{
    public virtual Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}
