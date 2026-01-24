namespace CaptureTool.Infrastructure.Interfaces.Loading;

public interface IAsyncLoadable<T> : IAsyncLoadableWithParam
{
    Task LoadAsync(T parameter, CancellationToken cancellationToken);
}