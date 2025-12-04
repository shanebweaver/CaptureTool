namespace CaptureTool.Services.Interfaces.Shutdown;

public partial interface IShutdownHandler
{
    void Shutdown(); 
    bool TryRestart();
}
