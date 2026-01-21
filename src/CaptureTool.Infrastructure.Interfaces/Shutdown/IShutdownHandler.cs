namespace CaptureTool.Infrastructure.Interfaces.Shutdown;

public partial interface IShutdownHandler
{
    bool IsShuttingDown { get; }
    void Shutdown(); 
    bool TryRestart();
}
