namespace CaptureTool.Infrastructure.Interfaces.Logging;

public interface ILogService
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();

    void LogInformation(string info);
    void LogWarning(string warning);
    void LogException(Exception e, string? message = null);

    event EventHandler<ILogEntry>? LogAdded;
    IEnumerable<ILogEntry> GetLogs();
    void ClearLogs();
}
