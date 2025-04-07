using System;
using System.Diagnostics;
using System.Threading;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CaptureTool.UI.Xaml.Views;

public sealed partial class ImageCanvasItemView : UserControl
{
    private static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
       nameof(ImageFile),
       typeof(ImageCanvasItemViewModel),
       typeof(ImageCanvasItemView),
       new PropertyMetadata(null, new PropertyChangedCallback(OnViewModelPropertyChanged)));

    public ImageCanvasItemViewModel ViewModel
    {
        get => (ImageCanvasItemViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvasItemView view)
        {
            if (e.OldValue is ImageCanvasItemViewModel oldVM)
            {
                oldVM.PropertyChanged -= view.OnViewModelPropertyChanged;
            }

            if (e.NewValue is ImageCanvasItemViewModel newVM)
            {
                newVM.PropertyChanged += view.OnViewModelPropertyChanged;
                view.HandleImageFileUpdated();
            }
        }
    }

    public ImageCanvasItemView()
    {
        InitializeComponent();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ImageCanvasItemViewModel.ImageFile):
                DispatcherQueue.TryEnqueue(HandleImageFileUpdated);
                break;
        }
    }

    private void HandleImageFileUpdated()
    {
        if (ViewModel.ImageFile != null)
        {
            try
            {
                BitmapImage bitmapImage = new(new Uri(ViewModel.ImageFile.Path, UriKind.RelativeOrAbsolute));
                CanvasImage.Source = bitmapImage;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }
    }
}
