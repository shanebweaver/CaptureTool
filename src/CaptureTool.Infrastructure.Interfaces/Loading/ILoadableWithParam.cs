namespace CaptureTool.Infrastructure.Interfaces.Loading;

public interface ILoadableWithParam : IHasLoadState, IHasParameterType
{
    void Load(object? parameter);
}