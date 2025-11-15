using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Common.Loading;

public interface IAsyncLoadable : IHasLoadState
{
    Task LoadAsync(CancellationToken cancellationToken);
}
