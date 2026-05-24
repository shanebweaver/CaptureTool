namespace CaptureTool.Infrastructure.Abstractions.Loading;

public interface ILoadableWithParam : IHasLoadState, IHasParameterType
{
    void Load(object? parameter);
}