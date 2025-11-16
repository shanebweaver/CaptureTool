using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Common.Loading;

public interface IAsyncLoadableWithParam : IHasLoadState, IHasParameterType
{
    Task LoadAsync(object? parameter, CancellationToken cancellationToken);
}