using CaptureTool.Infrastructure.Abstractions.Loading;

namespace CaptureTool.Infrastructure.Loading;

public abstract partial class Loadable : HasLoadStateBase, ILoadable
{
    public virtual void Load() => LoadingComplete();
}
