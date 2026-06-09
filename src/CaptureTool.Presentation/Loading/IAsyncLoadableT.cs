namespace CaptureTool.Presentation.Loading;

public interface IAsyncLoadable<T> : IAsyncLoadableWithParam
{
    Task LoadAsync(T parameter, CancellationToken cancellationToken);
}