namespace CaptureTool.Infrastructure.Interfaces.Loading;

public interface IAsyncLoadable : IHasLoadState
{
    Task LoadAsync(CancellationToken cancellationToken);
}
