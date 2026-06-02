namespace CaptureTool.Presentation.Loading;

public interface IAsyncLoadableWithParam : IHasLoadState, IHasParameterType
{
    Task LoadAsync(object? parameter, CancellationToken cancellationToken);
}