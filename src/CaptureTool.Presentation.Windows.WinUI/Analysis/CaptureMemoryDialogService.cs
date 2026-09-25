using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Analysis;

internal sealed class CaptureMemoryDialogService(MainWindowDialogCoordinator dialogs) : ICaptureMemoryPrompts
{
    private ResourceLoader? _resources;
    public XamlRoot? XamlRoot { get => dialogs.XamlRoot; set => dialogs.XamlRoot = value; }
    public async Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken cancellationToken)
    {
        var result = await dialogs.ShowAsync(() =>
        {
            string name = prompt.ToString();
            bool welcome = prompt is CaptureMemoryPrompt.Consent or CaptureMemoryPrompt.EnableScanning;
            var dialog = new ContentDialog
            {
                Title = Text("CaptureMemory_" + (welcome ? "EnableScanning" : name) + "Title"),
                Content = welcome
                    ? new CaptureTool.Presentation.Windows.WinUI.Xaml.Controls.CaptureAnalysisWelcome() : Text("CaptureMemory_" + name + "Content"),
                PrimaryButtonText = Text("CaptureMemory_" + name + "Accept"),
                CloseButtonText = Text(welcome ? "CaptureWelcome_NotNow" : "CaptureMemory_Cancel"),
                DefaultButton = prompt == CaptureMemoryPrompt.DeleteMetadata ? ContentDialogButton.Close : ContentDialogButton.Primary
            };
            AutomationProperties.SetAutomationId(dialog, "CaptureMemory" + name + "Dialog");
            return dialog;
        }, cancellationToken).ConfigureAwait(false);
        return result == ContentDialogResult.Primary;
    }
    private string Text(string key) => WinUIResourceLoader.GetString(ref _resources, key, key);
}
