using CaptureTool.Common.Loading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public abstract partial class AsyncLoadableViewModelBase : ViewModelBase, IAsyncLoadable
{
    private LoadState _loadState = LoadState.Loading;
    public LoadState LoadState
    {
        get => _loadState;
        set
        {
            Set(ref _loadState, value);
            RaisePropertyChanged(nameof(IsLoaded));
            RaisePropertyChanged(nameof(IsLoading));
        }
    }

    public bool IsLoading => LoadState == LoadState.Loading;
    public bool IsLoaded => LoadState == LoadState.Loaded;

    public virtual Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }

    protected void LoadingComplete()
    {
        LoadState = LoadState.Loaded;
    }

    public override void Dispose()
    {
        _loadState = LoadState.Error;
        GC.SuppressFinalize(this);
        base.Dispose();
    }
}
