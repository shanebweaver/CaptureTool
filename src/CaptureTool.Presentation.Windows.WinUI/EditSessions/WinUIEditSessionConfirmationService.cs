using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.EditSessions;

internal sealed class WinUIEditSessionConfirmationService : IEditSessionConfirmationService
{
    private ResourceLoader? _resourceLoader;

    public XamlRoot? XamlRoot { get; set; }

    public async Task<EditSessionLeaveDecision> ConfirmLeaveAsync(IEditableSession session, CancellationToken cancellationToken = default)
    {
        if (XamlRoot is null)
        {
            return EditSessionLeaveDecision.Cancel;
        }

        bool canSaveToSource = session is ISourceSaveableSession;
        EditSessionLeaveDecision decision = await ConfirmationCardPopupPresenter.ShowAsync(
            XamlRoot,
            EditSessionLeaveDecision.Cancel,
            complete => new ConfirmationCard
            {
                Title = GetString("EditSessionConfirmation_Title", "Leave editor?"),
                Message = GetString("EditSessionConfirmation_Content", "The current edit session has unsaved changes."),
                SecondaryButtonText = canSaveToSource
                    ? GetString("EditSessionConfirmation_SaveAsButton", "Save as")
                    : string.Empty,
                PrimaryButtonText = canSaveToSource
                    ? GetString("EditSessionConfirmation_SaveButton", "Save")
                    : GetString("EditSessionConfirmation_SaveAsButton", "Save as"),
                ConfirmButtonText = GetString("EditSessionConfirmation_DiscardButton", "Discard"),
                CancelButtonText = GetString("EditSessionConfirmation_CancelButton", "Cancel"),
                SecondaryCommand = canSaveToSource
                    ? new RelayCommand(() => complete(EditSessionLeaveDecision.SaveAs))
                    : null,
                PrimaryCommand = new RelayCommand(() => complete(
                    canSaveToSource
                        ? EditSessionLeaveDecision.SaveToSource
                        : EditSessionLeaveDecision.SaveAs)),
                ConfirmCommand = new RelayCommand(() => complete(EditSessionLeaveDecision.Discard)),
                CancelCommand = new RelayCommand(() => complete(EditSessionLeaveDecision.Cancel))
            });
        if (cancellationToken.IsCancellationRequested)
        {
            return EditSessionLeaveDecision.Cancel;
        }

        return decision;
    }

    private string GetString(string resourceKey, string fallback)
    {
        return WinUIResourceLoader.GetString(ref _resourceLoader, resourceKey, fallback);
    }
}
