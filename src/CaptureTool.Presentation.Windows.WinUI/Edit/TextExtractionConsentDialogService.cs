using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Edit;

internal sealed class TextExtractionConsentDialogService(MainWindowDialogCoordinator dialogs) : ITextExtractionConsentPrompt
{
    private ResourceLoader? _resources;
    public async Task<bool> ConfirmAsync(CancellationToken cancellationToken)
    {
        var result = await dialogs.ShowAsync(() =>
        {
            var dialog = new ContentDialog
            {
                Title = Text("AiFeatureConsentDialog_TextExtractionTitle"),
                Content = Text("AiFeatureConsentDialog_TextExtractionContent"),
                PrimaryButtonText = Text("AiFeatureConsentDialog_AllowButton"),
                SecondaryButtonText = Text("AiFeatureConsentDialog_DontAllowButton"),
                CloseButtonText = Text("AiFeatureConsentDialog_CancelButton"),
                DefaultButton = ContentDialogButton.Primary
            };
            AutomationProperties.SetAutomationId(dialog, "AiFeatureConsentDialog");
            return dialog;
        }, cancellationToken).ConfigureAwait(false);
        return result == ContentDialogResult.Primary;
    }
    private string Text(string key) => WinUIResourceLoader.GetString(ref _resources, key, key);
}
