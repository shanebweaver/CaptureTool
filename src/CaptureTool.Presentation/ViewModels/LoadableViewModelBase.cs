using CaptureTool.Presentation.Loading;

namespace CaptureTool.Presentation.ViewModels;

public abstract partial class LoadableViewModelBase : ViewModelBase, ILoadable
{
    public virtual void Load()
    {
        LoadingComplete();
    }
}
