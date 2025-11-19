using CaptureTool.Common.Loading;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Core;

public abstract partial class AsyncLoadableViewModelBase : ViewModelBase, IAsyncLoadable
{
    public virtual Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}
