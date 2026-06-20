namespace CaptureTool.Application.Abstractions.EditSessions;

public interface IActiveEditSessionService
{
    IEditableSession? CurrentSession { get; }
    void SetCurrentSession(IEditableSession session);
    void ClearCurrentSession(IEditableSession session);
}
