using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.ViewModels.Loading;

namespace CaptureTool.ViewModels;

public abstract partial class ViewModelBase : INotifyPropertyChanged, ILoadable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private LoadState _loadState;
    public LoadState LoadState
    {
        get => _loadState;
        set => Set(ref _loadState, value);
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

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new(propertyName));
        }
    }
}
