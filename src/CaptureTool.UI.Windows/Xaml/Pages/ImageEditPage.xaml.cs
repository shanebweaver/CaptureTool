using System;
using System.Drawing;
using System.Globalization;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
    }

    private void ViewModel_InvalidateCanvasRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ImageCanvas.InvalidateCanvas);
    }
}
