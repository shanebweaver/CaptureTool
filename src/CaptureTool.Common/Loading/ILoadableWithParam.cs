namespace CaptureTool.Common.Loading;

public interface ILoadableWithParam : IHasLoadState, IHasParameterType
{
    void Load(object? parameter);
}