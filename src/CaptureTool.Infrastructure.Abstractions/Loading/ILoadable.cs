namespace CaptureTool.Infrastructure.Abstractions.Loading;

public interface ILoadable : IHasLoadState
{
    void Load();
}