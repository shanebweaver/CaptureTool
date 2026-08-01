namespace CaptureTool.Application.Abstractions.EditSessions;

public interface ISourceSaveableSession : IEditableSession
{
    Task<bool> SaveToSourceAsync(CancellationToken cancellationToken = default);
}
