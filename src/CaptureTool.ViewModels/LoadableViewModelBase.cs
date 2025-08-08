using CaptureTool.Common.Loading;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public abstract partial class LoadableViewModelBase : ViewModelBase, ILoadable
{
    private LoadState _loadState;
    public LoadState LoadState
    {
        get => _loadState;
        set
        {
            Set(ref _loadState, value);
            RaisePropertyChanged(nameof(IsLoaded));
            RaisePropertyChanged(nameof(IsUnloaded));
        }
    }

    public bool IsUnloaded => LoadState == LoadState.Unloaded;
    public bool IsLoaded => LoadState == LoadState.Loaded;

    public virtual Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        LoadState = LoadState.Loaded;
        return Task.CompletedTask;
    }

    public virtual void Unload()
    {
        LoadState = LoadState.Unloaded;
    }

    protected void StartLoading()
    {
        LoadState = LoadState.Loading;
    }
}
