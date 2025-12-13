namespace CaptureTool.Services.Interfaces.Shutdown;

public partial interface IShutdownHandler
{
    event EventHandler? ShutdownRequested;
    bool IsShuttingDown { get; }
    void Shutdown(); 
    bool TryRestart();
    void NotifyMainWindowClosed();
}
