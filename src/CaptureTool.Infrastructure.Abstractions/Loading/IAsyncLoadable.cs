namespace CaptureTool.Infrastructure.Abstractions.Loading;

public interface IAsyncLoadable : IHasLoadState
{
    Task LoadAsync(CancellationToken cancellationToken);
}
