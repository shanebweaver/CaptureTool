using CaptureTool.Common.Loading;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CaptureTool.Common;

public abstract partial class ViewModelBase : HasLoadStateBase, IViewModel
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<LoadState>? LoadStateChanged;

    public override LoadState LoadState
    {
        get => field;
        protected set
        {
            Set(ref field, value);
            RaisePropertyChanged(nameof(IsLoaded));
            RaisePropertyChanged(nameof(IsLoading));
            LoadStateChanged?.Invoke(this, value);
        }
    }

    public bool IsReadyToLoad => LoadState == LoadState.Initial;

    protected void ThrowIfNotReadyToLoad()
    {
        if (!IsReadyToLoad)
        {
            throw new InvalidOperationException("Cannot load when not in the initial state.");
        }
    }

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            if (propertyName != null)
            {
                RaisePropertyChanged(propertyName);
            }
            return true;
        }

        return false;
    }

    protected void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    public virtual void Dispose()
    {
        LoadState = LoadState.Disposed;
        GC.SuppressFinalize(this);
    }
}