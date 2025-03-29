using System.Threading;
using System.Threading.Tasks;
using CaptureTool.ViewModels.Loading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptureTool.ViewModels;

public abstract partial class ViewModelBase : ObservableObject, ILoadable
{
    [ObservableProperty]
    private LoadState _loadState;

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
