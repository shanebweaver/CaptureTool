namespace CaptureTool.Application.Abstractions.Shutdown;

public partial interface IShutdownHandler
{
    bool IsShuttingDown { get; }
    void Shutdown();
    bool TryRestart();
}
