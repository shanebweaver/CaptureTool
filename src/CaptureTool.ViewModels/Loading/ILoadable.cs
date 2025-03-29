using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Loading;

public interface ILoadable
{
    LoadState LoadState { get; }

    Task LoadAsync(object? parameter, CancellationToken cancellationToken);

    void Unload();
}
