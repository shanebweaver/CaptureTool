using CaptureTool.Common.Loading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CaptureTool.Core;

public abstract partial class ViewModelBase : HasLoadStateBase, INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private LoadState _loadState = LoadState.Loading;
    public override LoadState LoadState
    {
        get => _loadState;
        protected set
        {
            Set(ref _loadState, value);
            RaisePropertyChanged(nameof(IsLoaded));
            RaisePropertyChanged(nameof(IsLoading));
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
        _loadState = LoadState.Disposed;
        GC.SuppressFinalize(this);
    }
}