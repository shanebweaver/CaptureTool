namespace CaptureTool.Presentation.Loading;

public interface ILoadable<T> : ILoadableWithParam
{
    void Load(T parameter);
}