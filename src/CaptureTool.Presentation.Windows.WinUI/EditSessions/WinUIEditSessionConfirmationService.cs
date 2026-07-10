using CommunityToolkit.Mvvm.Input;
using CaptureTool.Application.Abstractions.EditSessions;
using Microsoft.UI.Xaml;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.EditSessions;

internal sealed class WinUIEditSessionConfirmationService : IEditSessionConfirmationService
{
    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();

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
                Title = _resourceLoader.GetString("EditSessionConfirmation_Title"),
                Message = _resourceLoader.GetString("EditSessionConfirmation_Content"),
                PrimaryButtonText = _resourceLoader.GetString("EditSessionConfirmation_SaveAsButton"),
                ConfirmButtonText = _resourceLoader.GetString("EditSessionConfirmation_DiscardButton"),
                CancelButtonText = _resourceLoader.GetString("EditSessionConfirmation_CancelButton"),
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
}
