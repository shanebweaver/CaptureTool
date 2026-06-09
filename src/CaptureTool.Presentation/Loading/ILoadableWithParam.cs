namespace CaptureTool.Presentation.Loading;

public interface ILoadableWithParam : IHasLoadState, IHasParameterType
{
    void Load(object? parameter);
}