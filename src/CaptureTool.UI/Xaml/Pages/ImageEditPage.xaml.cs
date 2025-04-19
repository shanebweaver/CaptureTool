using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();

        ViewModel.CopyRequested += OnCopyRequested;
        ViewModel.PrintRequested += OnPrintRequested;
    }

    private async void OnCopyRequested(object? sender, EventArgs e)
    {
        var imageStream = await ImageCanvas.GetCanvasImageStreamAsync();
        if (imageStream != null)
        {
            DataPackage clipboardData = new();
            var streamReference = RandomAccessStreamReference.CreateFromStream(imageStream);
            clipboardData.SetBitmap(streamReference);
            Clipboard.SetContent(clipboardData);
        }
    }

    private void OnPrintRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _ = ImageCanvas.ShowPrintUIAsync());
    }
}
