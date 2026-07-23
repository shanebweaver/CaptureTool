using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Domain.Ai;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Edit;

internal sealed class AiFeatureConsentDialogService : IAiFeatureConsentDialogService
{
    private ResourceLoader? _resourceLoader;

    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> RequestConsentAsync(
        AiFeatureId featureId,
        CancellationToken cancellationToken = default)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        (string title, string content) = GetDialogText(featureId);
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = GetString("AiFeatureConsentDialog_AllowButton", "Allow"),
            SecondaryButtonText = GetString("AiFeatureConsentDialog_DontAllowButton", "Don't allow"),
            CloseButtonText = GetString("AiFeatureConsentDialog_CancelButton", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        AutomationProperties.SetAutomationId(dialog, "AiFeatureConsentDialog");

        ContentDialogResult result = await dialog.ShowAsync();
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return result == ContentDialogResult.Primary;
    }

    private (string Title, string Content) GetDialogText(AiFeatureId featureId)
    {
        return featureId switch
        {
            AiFeatureId.TextExtraction => (
                GetString("AiFeatureConsentDialog_TextExtractionTitle", "Allow Text Extraction?"),
                GetString(
                    "AiFeatureConsentDialog_TextExtractionContent",
                    "Text Extraction uses an on-device AI model to detect text and text locations in the current image.")),
            AiFeatureId.ImageSuperResolution => (
                "Allow Super Image Resolution?",
                "Super Image Resolution uses an on-device AI model to create a higher-resolution copy of the current image."),
            AiFeatureId.ImageDescription => (
                GetString("AiFeatureConsentDialog_ImageDescriptionTitle", "Allow Image Description?"),
                GetString(
                    "AiFeatureConsentDialog_ImageDescriptionContent",
                    "Image Description uses an on-device AI model to describe the current image.")),
            AiFeatureId.ImageForegroundExtraction => (
                GetString("AiFeatureConsentDialog_ImageForegroundExtractionTitle", "Allow Background Removal?"),
                GetString(
                    "AiFeatureConsentDialog_ImageForegroundExtractionContent",
                    "Background Removal uses an on-device AI model to isolate the subject you select and remove its background.")),
            _ => (
                "Allow AI feature?",
                "This feature uses an on-device AI model to process the current image.")
        };
    }

    private string GetString(string resourceKey, string fallback)
    {
        return WinUIResourceLoader.GetString(ref _resourceLoader, resourceKey, fallback);
    }
}
