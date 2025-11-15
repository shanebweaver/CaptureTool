using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Common.Loading;

public abstract partial class AsyncLoadable : HasLoadStateBase, IAsyncLoadable
{
    public virtual Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}
