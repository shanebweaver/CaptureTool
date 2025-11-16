namespace CaptureTool.Common.Loading;

public abstract partial class HasLoadStateBase : IHasLoadState
{
    public virtual LoadState LoadState { get; protected set; }

    public bool IsLoading => LoadState == LoadState.Loading;
    public bool IsLoaded => LoadState == LoadState.Loaded;

    protected void StartLoading() => LoadState = LoadState.Loading;
    protected void LoadingComplete() => LoadState = LoadState.Loaded;
    protected void LoadingError() => LoadState = LoadState.Error;
}