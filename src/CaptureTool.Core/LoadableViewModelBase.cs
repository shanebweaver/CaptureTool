using CaptureTool.Common.Loading;

namespace CaptureTool.Core;

public abstract partial class LoadableViewModelBase : ViewModelBase, ILoadable
{
    public virtual void Load()
    {
        LoadingComplete();
    }
}
