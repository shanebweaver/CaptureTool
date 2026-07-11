using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Domain.Ai;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Edit;

internal sealed class AiFeatureConsentDialogService : IAiFeatureConsentDialogService
{
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
            PrimaryButtonText = "Allow",
            SecondaryButtonText = "Don't allow",
            CloseButtonText = "Cancel",
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

    private static (string Title, string Content) GetDialogText(AiFeatureId featureId)
    {
        return featureId switch
        {
            AiFeatureId.TextExtraction => (
                "Allow Text Extraction?",
                "Text Extraction uses an on-device AI model to detect text and text locations in the current image."),
            AiFeatureId.ImageSuperResolution => (
                "Allow Super Image Resolution?",
                "Super Image Resolution uses an on-device AI model to create a higher-resolution copy of the current image."),
            _ => (
                "Allow AI feature?",
                "This feature uses an on-device AI model to process the current image.")
        };
    }
}
