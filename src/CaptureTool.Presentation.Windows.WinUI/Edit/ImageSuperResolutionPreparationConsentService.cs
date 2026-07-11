using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Edit;

internal sealed class ImageSuperResolutionPreparationConsentService : IImageSuperResolutionPreparationConsentService
{
    private ResourceLoader? _resourceLoader;

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
            Title = GetString("ImageSuperResolutionPreparation_Title", "Prepare image?"),
            Content = GetString("ImageSuperResolutionPreparation_Content", "The image will be prepared for super resolution."),
            PrimaryButtonText = GetString("ImageSuperResolutionPreparation_ContinueButton", "Continue"),
            CloseButtonText = GetString("ImageSuperResolutionPreparation_CancelButton", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return result == ContentDialogResult.Primary;
    }

    private string GetString(string resourceKey, string fallback)
    {
        return WinUIResourceLoader.GetString(ref _resourceLoader, resourceKey, fallback);
    }
}
