namespace CaptureTool.Infrastructure.Interfaces.Loading;

public interface ILoadable : IHasLoadState
{
    void Load();
}