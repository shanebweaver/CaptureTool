using CaptureTool.Infrastructure.Interfaces.Loading;

namespace CaptureTool.Infrastructure.Implementations.Loading;

public abstract partial class Loadable : HasLoadStateBase, ILoadable
{
    public virtual void Load() => LoadingComplete();
}
