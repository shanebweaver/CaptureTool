namespace CaptureTool.Application.Abstractions.EditSessions;

public interface IEditSessionConfirmationService
{
    Task<EditSessionLeaveDecision> ConfirmLeaveAsync(IEditableSession session, CancellationToken cancellationToken = default);
}
