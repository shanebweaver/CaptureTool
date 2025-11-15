using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Common.Loading;

public interface IAsyncLoadable<T> : IAsyncLoadableWithParam
{
    Task LoadAsync(T parameter, CancellationToken cancellationToken);
}