using CommunityToolkit.Mvvm.Input;
using CaptureTool.Application.Abstractions.EditSessions;
using Microsoft.UI.Xaml;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
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

        EditSessionLeaveDecision decision = await ConfirmationCardPopupPresenter.ShowAsync(
            XamlRoot,
            EditSessionLeaveDecision.Cancel,
            complete => new ConfirmationCard
            {
                Title = GetString("EditSessionConfirmation_Title", "Leave editor?"),
                Message = GetString("EditSessionConfirmation_Content", "The current edit session has unsaved changes."),
                PrimaryButtonText = GetString("EditSessionConfirmation_SaveAsButton", "Save as"),
                ConfirmButtonText = GetString("EditSessionConfirmation_DiscardButton", "Discard"),
                CancelButtonText = GetString("EditSessionConfirmation_CancelButton", "Cancel"),
                PrimaryCommand = new RelayCommand(() => complete(EditSessionLeaveDecision.Save)),
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
