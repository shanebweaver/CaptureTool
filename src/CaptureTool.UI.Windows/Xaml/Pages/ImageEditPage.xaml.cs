using System;
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

    private void ImageEditPage_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        InvalidateCanvas();
    }

    private void ViewModel_InvalidateCanvasRequested(object? sender, EventArgs e)
    {
        InvalidateCanvas();
    }

    private void InvalidateCanvas()
    {
        DispatcherQueue.TryEnqueue(ImageCanvas.InvalidateCanvas);
    }

    private void ImageCanvas_InteractionComplete(object sender, Rectangle e)
    {
        ViewModel.OnCropInteractionComplete(e);
    }
}
