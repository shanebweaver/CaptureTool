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
        ContentDialog dialog = CreateDialog();
        ContentDialogResult result = await ShowDialogAsync(dialog);
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return result == ContentDialogResult.Primary;
    }

    private ContentDialog CreateDialog()
    {
        return new()
        {
            Title = _resourceLoader.GetString("CaptureDiscardConfirmation_Title"),
            Content = _resourceLoader.GetString("CaptureDiscardConfirmation_Content"),
            PrimaryButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_DiscardButton"),
            CloseButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };
    }

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (CanShowInlineDialog())
        {
            dialog.XamlRoot = XamlRoot;
            return await dialog.ShowAsync();
        }

        var hostWindow = new CaptureDiscardDialogHostWindow(DialogHostBounds);
        return await hostWindow.ShowContentDialogAsync(dialog);
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
