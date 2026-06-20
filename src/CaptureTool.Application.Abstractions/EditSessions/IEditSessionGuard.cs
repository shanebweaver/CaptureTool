namespace CaptureTool.Application.Abstractions.EditSessions;

public interface IEditSessionGuard
{
    Task<bool> CanLeaveCurrentSessionAsync(CancellationToken cancellationToken = default);
}
