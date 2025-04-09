using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Xaml.Views.ImageEditCanvas;

public abstract partial class CanvasItemViewBase<VM> : UserControl where VM : CanvasItemViewModel
{
    private static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
       nameof(ViewModel),
       typeof(VM),
       typeof(CanvasItemViewBase<VM>),
       new PropertyMetadata(null, new PropertyChangedCallback(OnViewModelPropertyChanged)));

    public VM ViewModel
    {
        get => (VM)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CanvasItemViewBase<VM> view)
        {
            view.OnViewModelPropertyChanged(e);
        }
    }

    protected virtual void OnViewModelPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
    }
}
