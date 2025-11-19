using CaptureTool.Common.Loading;

namespace CaptureTool.Common;

public abstract partial class LoadableViewModelBase : ViewModelBase, ILoadable
{
    public virtual void Load()
    {
        LoadingComplete();
    }
}
