namespace CaptureTool.Common.Loading;

public abstract partial class Loadable : HasLoadStateBase, ILoadable
{
    public virtual void Load() => LoadingComplete();
}
