using CaptureTool.Common.Loading;
using System;

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
        }
    }

    public bool IsLoaded => LoadState == LoadState.Loaded;

    public virtual void Load(object? parameter)
    {
        LoadingComplete();
    }

    protected void StartLoading()
    {
        LoadState = LoadState.Loading;
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
