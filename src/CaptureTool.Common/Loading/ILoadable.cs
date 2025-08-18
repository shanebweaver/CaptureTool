namespace CaptureTool.Common.Loading;

public interface ILoadable : IUnloadable
{
    LoadState LoadState { get; }

    bool IsLoaded => LoadState == LoadState.Loaded;
    bool IsUnloaded => LoadState == LoadState.Unloaded;

    void Load(object? parameter);

}
