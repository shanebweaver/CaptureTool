using CaptureTool.Services.Interfaces.Logging;
using System.Diagnostics;

namespace CaptureTool.Services.Implementations.Logging;

public class DebugLogService : ILogService
{
    public void LogException(Exception e, string? message = null)
    {
        Debug.Write("ERROR: ");
        if (message != null)
        {
            Debug.WriteLine(message);
        }

        Debug.WriteLine(e.Message);
        if (!string.IsNullOrEmpty(e.StackTrace))
        {
            Debug.WriteLine(e.StackTrace);
        }
    }

    public void LogInformation(string info)
    {
        Debug.Write("INFO: ");
        Debug.WriteLine(info);
    }

    public void LogWarning(string warning)
    {
        Debug.Write("WARNING: ");
        Debug.WriteLine(warning);
    }
}
