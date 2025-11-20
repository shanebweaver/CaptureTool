namespace CaptureTool.Services.Interfaces.Logging;

public interface ILogService
{
    void LogInformation(string info);
    void LogWarning(string warning);
    void LogException(Exception e, string? message = null);
}
