namespace CaptureTool.Infrastructure.Abstractions.Loading;

public interface ILoadable<T> : ILoadableWithParam
{
    void Load(T parameter);
}