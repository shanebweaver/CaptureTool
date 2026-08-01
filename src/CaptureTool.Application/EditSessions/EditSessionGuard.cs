using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.EditSessions;

internal sealed class EditSessionGuard : IEditSessionGuard
{
    private readonly IActiveEditSessionService _activeEditSessionService;
    private readonly IEditSessionConfirmationService _confirmationService;
    private readonly ISettingsService _settingsService;

    public EditSessionGuard(
        IActiveEditSessionService activeEditSessionService,
        IEditSessionConfirmationService confirmationService,
        ISettingsService settingsService)
    {
        _activeEditSessionService = activeEditSessionService;
        _confirmationService = confirmationService;
        _settingsService = settingsService;
    }

    public async Task<bool> CanLeaveCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        IEditableSession? session = _activeEditSessionService.CurrentSession;
        if (session is null || !session.HasUnsavedChanges)
        {
            return true;
        }

        bool shouldWarn = _settingsService.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard);
        if (!shouldWarn)
        {
            return true;
        }

        EditSessionLeaveDecision decision = await _confirmationService.ConfirmLeaveAsync(session, cancellationToken);
        return decision switch
        {
            EditSessionLeaveDecision.SaveToSource when session is ISourceSaveableSession sourceSaveableSession
                => await sourceSaveableSession.SaveToSourceAsync(cancellationToken),
            EditSessionLeaveDecision.SaveAs => await session.SaveAsync(cancellationToken),
            EditSessionLeaveDecision.Discard => true,
            _ => false,
        };
    }
}
