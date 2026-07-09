using CaptureTool.Application.Abstractions.Capture;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class WinUICaptureDiscardConfirmationService : ICaptureDiscardConfirmationService
{
    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();

    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmDiscardActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("CaptureDiscardConfirmation_Title"),
            Content = _resourceLoader.GetString("CaptureDiscardConfirmation_Content"),
            PrimaryButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_DiscardButton"),
            CloseButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return result == ContentDialogResult.Primary;
    }
}
