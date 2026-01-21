using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.Infrastructure.Implementations.Logging;

public class ShortTermMemoryLogService : LogServiceBase
{
    private static readonly TimeSpan MemorySpan = TimeSpan.FromMinutes(5);
    private static readonly int MaxLogEntries = 1000;

    private readonly Lock _lock = new();

    protected override void AddLogEntry(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (_lock)
        {
            // Add new entry
            base.AddLogEntry(message);

            // Remove entries older than MemorySpan
            var cutoff = DateTime.Now - MemorySpan;
            while (LogEntries.First != null && LogEntries.First.Value.Timestamp < cutoff)
            {
                LogEntries.RemoveFirst();
            }

            // Remove excess entries if over MaxLogEntries
            while (LogEntries.Count > MaxLogEntries)
            {
                LogEntries.RemoveFirst();
            }
        }
    }

    public override IEnumerable<ILogEntry> GetLogs()
    {
        lock (_lock)
        {
            return base.GetLogs();
        }
    }

    public override void ClearLogs()
    {
        lock (_lock)
        {
            base.ClearLogs();
        }
    }
}
