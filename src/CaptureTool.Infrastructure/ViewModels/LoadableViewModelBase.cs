using CaptureTool.Infrastructure.Abstractions.Loading;

namespace CaptureTool.Infrastructure.ViewModels;

public abstract partial class LoadableViewModelBase : ViewModelBase, ILoadable
{
    public virtual void Load()
    {
        LoadingComplete();
    }
}
