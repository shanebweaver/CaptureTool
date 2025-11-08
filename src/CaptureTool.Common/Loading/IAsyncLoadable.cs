using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Common.Loading;

public interface IAsyncLoadable
{
    LoadState LoadState { get; }

    bool IsLoaded => LoadState == LoadState.Loaded;

    Task LoadAsync(object? parameter, CancellationToken cancellationToken);
}
