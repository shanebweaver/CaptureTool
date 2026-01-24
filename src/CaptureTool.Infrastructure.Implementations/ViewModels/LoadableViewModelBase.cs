using CaptureTool.Infrastructure.Interfaces.Loading;

namespace CaptureTool.Infrastructure.Implementations.ViewModels;

public abstract partial class LoadableViewModelBase : ViewModelBase, ILoadable
{
    public virtual void Load()
    {
        LoadingComplete();
    }
}
