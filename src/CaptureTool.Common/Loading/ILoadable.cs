namespace CaptureTool.Common.Loading;

public interface ILoadable
{
    LoadState LoadState { get; }

    bool IsLoaded => LoadState == LoadState.Loaded;

    void Load(object? parameter);
}
