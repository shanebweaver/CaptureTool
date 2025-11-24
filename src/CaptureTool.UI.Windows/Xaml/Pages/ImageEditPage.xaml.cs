using Microsoft.UI.Xaml;
using System.Drawing;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
        SizeChanged += ImageEditPage_SizeChanged;
    }

    private void ImageEditPage_SizeChanged(object _, SizeChangedEventArgs __)
    {
        InvalidateCanvas();
    }

    private void ViewModel_InvalidateCanvasRequested(object? _, EventArgs __)
    {
        InvalidateCanvas();
    }

    private void InvalidateCanvas()
    {
        DispatcherQueue.TryEnqueue(ImageCanvas.InvalidateCanvas);
    }

    private void ImageCanvas_InteractionComplete(object _, Rectangle e)
    {
        ViewModel.OnCropInteractionComplete(e);
    }

    private void ImageCanvas_CropRectChanged(object _, Rectangle e)
    {
        ViewModel.UpdateCropRectCommand.Execute(e);
    }
}
