namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
    }

    private void ViewModel_InvalidateCanvasRequested(object? sender, System.EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ImageCanvas.InvalidateCanvas);
    }
}
