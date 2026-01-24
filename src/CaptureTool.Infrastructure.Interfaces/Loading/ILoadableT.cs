namespace CaptureTool.Infrastructure.Interfaces.Loading;

public interface ILoadable<T> : ILoadableWithParam
{
    void Load(T parameter);
}