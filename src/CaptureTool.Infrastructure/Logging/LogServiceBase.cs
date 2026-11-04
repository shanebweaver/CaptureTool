using CaptureTool.Application.Abstractions.Logging;
using System.Text;

namespace CaptureTool.Infrastructure.Logging;

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
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"Exception type: {e.GetType().FullName}. HRESULT: 0x{e.HResult:X8}.");

        if (message != null)
        {
            stringBuilder.AppendLine(message);
        }
        // ToString preserves inner exceptions (including AggregateException
        // branches), which often contain the original native/COM failure.
        stringBuilder.AppendLine(e.ToString());

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
