using CaptureTool.Application.Abstractions.EditSessions;

namespace CaptureTool.Application.EditSessions;

public sealed class ActiveEditSessionService : IActiveEditSessionService
{
    private readonly Lock _lock = new();
    private IEditableSession? _currentSession;

    public IEditableSession? CurrentSession
    {
        get
        {
            lock (_lock)
            {
                return _currentSession;
            }
        }
    }

    public void SetCurrentSession(IEditableSession session)
    {
        lock (_lock)
        {
            _currentSession = session;
        }
    }

    public void ClearCurrentSession(IEditableSession session)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_currentSession, session))
            {
                _currentSession = null;
            }
        }
    }
}
