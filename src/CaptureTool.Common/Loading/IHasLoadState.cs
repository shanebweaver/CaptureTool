namespace CaptureTool.Common.Loading;

public interface IHasLoadState
{
    LoadState LoadState { get; }
    bool IsLoaded => LoadState == LoadState.Loaded;
}
