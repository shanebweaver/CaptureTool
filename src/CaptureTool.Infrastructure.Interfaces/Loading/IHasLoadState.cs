namespace CaptureTool.Infrastructure.Interfaces.Loading;

public interface IHasLoadState
{
    LoadState LoadState { get; }
    bool IsLoaded => LoadState == LoadState.Loaded;
    bool IsLoading => LoadState == LoadState.Loading;
}
