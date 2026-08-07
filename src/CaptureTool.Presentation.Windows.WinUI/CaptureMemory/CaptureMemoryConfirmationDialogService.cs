using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.CaptureMemory;

internal sealed class CaptureMemoryConfirmationDialogService :
    ICaptureAnalysisSettingsConfirmationDialogService
{
    private ResourceLoader? _resourceLoader;

    public XamlRoot? XamlRoot { get; set; }

    public async ValueTask<CaptureAnalysisConfirmationDecision> ConfirmAsync(
        CaptureAnalysisSettingsConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (XamlRoot == null || cancellationToken.IsCancellationRequested)
        {
            return CaptureAnalysisConfirmationDecision.Cancelled;
        }

        DialogText text = GetDialogText(request.Action);
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = text.Title,
            Content = text.Content,
            PrimaryButtonText = text.PrimaryButton,
            CloseButtonText = GetString(
                "CaptureMemory_Confirmation_CancelButton",
                "Cancel"),
            DefaultButton = ContentDialogButton.None,
        };
        AutomationProperties.SetAutomationId(dialog, "CaptureMemoryConfirmationDialog");

        ContentDialogResult result = await dialog.ShowAsync();
        return !cancellationToken.IsCancellationRequested && result == ContentDialogResult.Primary
            ? CaptureAnalysisConfirmationDecision.Confirmed
            : CaptureAnalysisConfirmationDecision.Cancelled;
    }

    private DialogText GetDialogText(CaptureAnalysisSettingsAction action)
    {
        return action switch
        {
            CaptureAnalysisSettingsAction.AuthorizeExistingCaptureBackfill => new(
                GetString(
                    "CaptureMemory_Confirmation_BackfillTitle",
                    "Analyze existing captures?"),
                GetString(
                    "CaptureMemory_Confirmation_BackfillContent",
                    "Capture Tool will read eligible existing capture sources and use the authorized on-device AI models. Original captures are not modified."),
                GetString(
                    "CaptureMemory_Confirmation_BackfillButton",
                    "Analyze existing captures")),
            CaptureAnalysisSettingsAction.StopAnalyzingNewCaptures => new(
                GetString(
                    "CaptureMemory_Confirmation_StopTitle",
                    "Stop analyzing new captures?"),
                GetString(
                    "CaptureMemory_Confirmation_StopContent",
                    "New captures will no longer be analyzed. Existing app-managed metadata and search results remain. Original captures are unchanged."),
                GetString(
                    "CaptureMemory_Confirmation_StopButton",
                    "Stop analyzing")),
            CaptureAnalysisSettingsAction.TurnOffAndErase => new(
                GetString(
                    "CaptureMemory_Confirmation_EraseTitle",
                    "Turn off Capture Memory and erase its data?"),
                GetString(
                    "CaptureMemory_Confirmation_EraseContent",
                    "This stops all Capture Memory analysis and erases app-managed metadata, jobs, and search data. Original capture files and separately saved exports are not deleted."),
                GetString(
                    "CaptureMemory_Confirmation_EraseButton",
                    "Turn off and erase")),
            CaptureAnalysisSettingsAction.ClearMemory => new(
                GetString(
                    "CaptureMemory_Confirmation_ClearTitle",
                    "Clear Capture Memory?"),
                GetString(
                    "CaptureMemory_Confirmation_ClearContent",
                    "This erases app-managed metadata and search data for enrolled captures. Analysis of new captures remains on. Original capture files are not deleted."),
                GetString(
                    "CaptureMemory_Confirmation_ClearButton",
                    "Clear Memory")),
            CaptureAnalysisSettingsAction.RebuildSearchIndex => new(
                GetString(
                    "CaptureMemory_Confirmation_RebuildTitle",
                    "Rebuild the search index?"),
                GetString(
                    "CaptureMemory_Confirmation_RebuildContent",
                    "This recreates disposable search data from existing app-managed metadata. It does not run AI models, read capture sources, or modify original files."),
                GetString(
                    "CaptureMemory_Confirmation_RebuildButton",
                    "Rebuild index")),
            CaptureAnalysisSettingsAction.ReanalyzeCaptures => new(
                GetString(
                    "CaptureMemory_Confirmation_ReanalyzeTitle",
                    "Reanalyze captures?"),
                GetString(
                    "CaptureMemory_Confirmation_ReanalyzeContent",
                    "Capture Tool may prepare authorized on-device AI models and read enrolled capture sources again. Original captures are not modified."),
                GetString(
                    "CaptureMemory_Confirmation_ReanalyzeButton",
                    "Reanalyze captures")),
            CaptureAnalysisSettingsAction.RemoveFromMemory => new(
                GetString(
                    "CaptureMemory_Confirmation_RemoveTitle",
                    "Remove this capture from Memory?"),
                GetString(
                    "CaptureMemory_Confirmation_RemoveContent",
                    "This removes the capture's app-managed metadata and search history. The capture file is not deleted."),
                GetString(
                    "CaptureMemory_Confirmation_RemoveButton",
                    "Remove from Memory")),
            CaptureAnalysisSettingsAction.DeleteCapture => new(
                GetString(
                    "CaptureMemory_Confirmation_DeleteTitle",
                    "Delete this capture?"),
                GetString(
                    "CaptureMemory_Confirmation_DeleteContent",
                    "This permanently deletes the app-owned retained source and removes its app-managed Memory data. Any separately saved or exported copy remains."),
                GetString(
                    "CaptureMemory_Confirmation_DeleteButton",
                    "Delete capture")),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private string GetString(string key, string fallback)
    {
        return WinUIResourceLoader.GetString(ref _resourceLoader, key, fallback);
    }

    private readonly record struct DialogText(
        string Title,
        string Content,
        string PrimaryButton);
}
