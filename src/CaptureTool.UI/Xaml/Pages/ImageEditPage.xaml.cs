using System;
using System.Diagnostics;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ImageEditPageViewModel.ImageFile):

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
                Brush backgroundBrush = new ImageBrush() { ImageSource = bitmapImage };
                Background = backgroundBrush;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
