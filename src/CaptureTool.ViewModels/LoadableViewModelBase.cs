using CaptureTool.Common.Loading;

namespace CaptureTool.ViewModels;

public abstract partial class LoadableViewModelBase : ViewModelBase, ILoadable
{
    public virtual void Load()
    {
        LoadingComplete();
    }
}
