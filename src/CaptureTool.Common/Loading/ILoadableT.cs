namespace CaptureTool.Common.Loading;

public interface ILoadable<T> : ILoadableWithParam
{
    void Load(T parameter);
}