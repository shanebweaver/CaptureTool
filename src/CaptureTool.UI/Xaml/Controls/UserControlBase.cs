using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CaptureTool.UI.Xaml.Controls;

public abstract partial class UserControlBase : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected T Get<T>(DependencyProperty dp)
    {
        return (T)GetValue(dp);
    }

    protected void Set<T>(DependencyProperty dp, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(value, (T)GetValue(dp)))
        {
            SetValue(dp, value);
            PropertyChanged?.Invoke(this, new(propertyName));
        }
    }
}
