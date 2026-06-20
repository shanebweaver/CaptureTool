using CaptureTool.Application.Abstractions.EditSessions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("EditSessionConfirmation_Title"),
            Content = _resourceLoader.GetString("EditSessionConfirmation_Content"),
            PrimaryButtonText = _resourceLoader.GetString("EditSessionConfirmation_SaveAsButton"),
            SecondaryButtonText = _resourceLoader.GetString("EditSessionConfirmation_DiscardButton"),
            CloseButtonText = _resourceLoader.GetString("EditSessionConfirmation_CancelButton"),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (cancellationToken.IsCancellationRequested)
        {
            return EditSessionLeaveDecision.Cancel;
        }

        return result switch
        {
            ContentDialogResult.Primary => EditSessionLeaveDecision.Save,
            ContentDialogResult.Secondary => EditSessionLeaveDecision.Discard,
            _ => EditSessionLeaveDecision.Cancel
        };
    }
}
