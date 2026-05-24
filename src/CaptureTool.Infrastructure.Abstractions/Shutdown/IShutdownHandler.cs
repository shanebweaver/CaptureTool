namespace CaptureTool.Infrastructure.Abstractions.Shutdown;

public partial interface IShutdownHandler
{
    bool IsShuttingDown { get; }
    void Shutdown();
    bool TryRestart();
}
