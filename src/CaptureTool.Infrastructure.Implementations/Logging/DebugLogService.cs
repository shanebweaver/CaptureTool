namespace CaptureTool.Infrastructure.Implementations.Logging;

public sealed partial class DebugLogService : LogServiceBase
{
    protected override void AddLogEntry(string message)
    {
        base.AddLogEntry(message);
        System.Diagnostics.Debug.WriteLine(message);
    }
}
