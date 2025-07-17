using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CaptureTool.ViewModels;

public abstract partial class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new(propertyName));
            return true;
        }

        return false;
    }
}
