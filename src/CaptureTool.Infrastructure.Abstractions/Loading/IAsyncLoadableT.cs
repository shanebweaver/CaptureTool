namespace CaptureTool.Infrastructure.Abstractions.Loading;

public interface IAsyncLoadable<T> : IAsyncLoadableWithParam
{
    Task LoadAsync(T parameter, CancellationToken cancellationToken);
}