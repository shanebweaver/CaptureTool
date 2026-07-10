using CaptureTool.Application.Abstractions.Capture;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;
using Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class WinUICaptureDiscardConfirmationService : ICaptureDiscardConfirmationService
{
    private const double MinimumInlineDialogWidth = 360;
    private const double MinimumInlineDialogHeight = 220;

    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();

    public XamlRoot? XamlRoot { get; set; }
    public Rectangle? DialogHostBounds { get; set; }

    public async Task<bool> ConfirmDiscardActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        string title = _resourceLoader.GetString("CaptureDiscardConfirmation_Title");
        string content = _resourceLoader.GetString("CaptureDiscardConfirmation_Content");
        string discardButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_DiscardButton");
        string cancelButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_CancelButton");

        bool shouldDiscardCapture = await ShowConfirmationAsync(
            title,
            content,
            discardButtonText,
            cancelButtonText);
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return shouldDiscardCapture;
    }

    private async Task<bool> ShowConfirmationAsync(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        if (CanShowInlineDialog())
        {
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = content,
                PrimaryButtonText = discardButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        using var overlayHost = new CaptureDiscardConfirmationOverlayHost(DialogHostBounds);
        return await overlayHost.ShowConfirmationAsync(
            title,
            content,
            discardButtonText,
            cancelButtonText);
    }

    private bool CanShowInlineDialog()
    {
        if (XamlRoot is null)
        {
            return false;
        }

        return XamlRoot.Size.Width >= MinimumInlineDialogWidth
            && XamlRoot.Size.Height >= MinimumInlineDialogHeight;
    }
}
