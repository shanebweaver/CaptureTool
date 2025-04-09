using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using CaptureTool.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CaptureTool.UI.Xaml.Views.ImageEditCanvas;

public sealed partial class ImageCanvasItemView : ImageCanvasItemViewBase
{
    public ImageCanvasItemView()
    {
        InitializeComponent();
    }

    protected override void OnViewModelPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ImageCanvasItemViewModel oldVM)
        {
            oldVM.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is ImageCanvasItemViewModel newVM)
        {
            newVM.PropertyChanged += ViewModel_PropertyChanged;
            HandleImageFileUpdated();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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
