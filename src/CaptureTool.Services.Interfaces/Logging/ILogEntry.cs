
namespace CaptureTool.Services.Interfaces.Logging;

public interface ILogEntry
{
    string Message { get; }
    DateTime Timestamp { get; }

    string ToString();
}