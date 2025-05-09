using System;

namespace CaptureTool.Services.Logging;

public interface ILogService
{
    void LogInformation(string info);
    void LogWarning(string warning);
    void LogException(Exception e, string? message = null);
}
