using System;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();

        ViewModel.CopyRequested += OnCopyRequested;
        ViewModel.PrintRequested += OnPrintRequested;
    }

    private void OnCopyRequested(object? sender, EventArgs e)
    {
        ImageCanvas.CopyImageToClipboard();
    }

    private void OnPrintRequested(object? sender, EventArgs e)
    {
        ImageCanvas.ShowPrintUI();
    }
}
