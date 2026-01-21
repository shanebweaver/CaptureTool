using CaptureTool.Infrastructure.Interfaces.Logging;
using System.Text;

namespace CaptureTool.Infrastructure.Implementations.Logging;

public abstract class LogServiceBase : ILogService
{
    protected readonly LinkedList<LogEntry> LogEntries = new();

    public event EventHandler<ILogEntry>? LogAdded;

    public bool IsEnabled { get; private set; }

    public virtual IEnumerable<ILogEntry> GetLogs()
    {
        return [.. LogEntries.Reverse()];
    }

    public void LogInformation(string info)
    {
        AddLogEntry($"INFO: {info}");
    }

    public void LogWarning(string warning)
    {
        AddLogEntry($"WARNING: {warning}");
    }

    public void LogException(Exception e, string? message = null)
    {
        StringBuilder stringBuilder = new($"ERROR: {e.Message}");

        if (message != null)
        {
            stringBuilder.AppendLine(message);
        }
        if (!string.IsNullOrEmpty(e.StackTrace))
        {
            stringBuilder.AppendLine(e.StackTrace);
        }

        AddLogEntry(stringBuilder.ToString());
    }

    protected virtual void AddLogEntry(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        var logEntry = new LogEntry(message);
        LogEntries.AddLast(logEntry);
        LogAdded?.Invoke(this, logEntry);
    }

    public virtual void Enable()
    {
        IsEnabled = true;
    }

    public virtual void Disable()
    {
        IsEnabled = false;
    }

    public virtual void ClearLogs()
    {
        LogEntries.Clear();
    }
}
