namespace CaptureTool.Common.Loading;

public interface ILoadable : IHasLoadState
{
    void Load();
}