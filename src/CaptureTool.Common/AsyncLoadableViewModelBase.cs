using CaptureTool.Common.Loading;

namespace CaptureTool.Common;

public abstract partial class AsyncLoadableViewModelBase : ViewModelBase, IAsyncLoadable
{
    public virtual Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}
