namespace CaptureTool.Presentation.Loading;

public interface ILoadable : IHasLoadState
{
    void Load();
}