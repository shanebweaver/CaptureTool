using CaptureTool.Application.Abstractions.EditSessions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.EditSessions;

internal sealed class WinUIEditSessionConfirmationService : IEditSessionConfirmationService
{
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
            Title = "Save changes?",
            Content = $"You have unsaved changes in {session.EditSessionName}. Save them before leaving?",
            PrimaryButtonText = "Save as",
            SecondaryButtonText = "Discard",
            CloseButtonText = "Cancel",
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
