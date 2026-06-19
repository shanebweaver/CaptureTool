namespace CaptureTool.Application.Abstractions.EditSessions;

public interface IEditableSession
{
    string EditSessionName { get; }
    bool HasUnsavedChanges { get; }
    Task<bool> SaveAsync(CancellationToken cancellationToken = default);
    Task AutoSaveAsync(CancellationToken cancellationToken = default);
}
