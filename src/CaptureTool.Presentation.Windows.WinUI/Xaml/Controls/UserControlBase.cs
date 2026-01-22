using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public abstract partial class UserControlBase : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected T Get<T>(DependencyProperty dp)
    {
        return (T)GetValue(dp);
    }

    protected void Set<T>(DependencyProperty dp, T value, [CallerMemberName] string? propertyName = null)
    {
        var current = GetValue(dp);
        if (current == DependencyProperty.UnsetValue || current is not T t || !EqualityComparer<T>.Default.Equals(value, t))
        {
            SetValue(dp, value);
            RaisePropertyChanged(propertyName);
        }
    }

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(value, field))
        {
            field = value;
            RaisePropertyChanged(propertyName);
        }
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (!string.IsNullOrEmpty(propertyName))
        {
            PropertyChanged?.Invoke(this, new(propertyName));
        }
    }
}
