namespace CaptureTool.Presentation.Loading;

public interface IAsyncLoadable : IHasLoadState
{
    Task LoadAsync(CancellationToken cancellationToken);
}
