using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Edit;

internal sealed class ImageSuperResolutionPreparationConsentService : IImageSuperResolutionPreparationConsentService
{
    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();

    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmPreparationAsync(CancellationToken cancellationToken = default)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("ImageSuperResolutionPreparation_Title"),
            Content = _resourceLoader.GetString("ImageSuperResolutionPreparation_Content"),
            PrimaryButtonText = _resourceLoader.GetString("ImageSuperResolutionPreparation_ContinueButton"),
            CloseButtonText = _resourceLoader.GetString("ImageSuperResolutionPreparation_CancelButton"),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return result == ContentDialogResult.Primary;
    }
}
