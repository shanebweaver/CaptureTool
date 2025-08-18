using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Common.Loading;

public interface IAsyncLoadable : IUnloadable
{
    LoadState LoadState { get; }

    bool IsLoaded => LoadState == LoadState.Loaded;
    bool IsUnloaded => LoadState == LoadState.Unloaded;

    Task LoadAsync(object? parameter, CancellationToken cancellationToken);
}
