namespace CaptureTool.Services.Interfaces.AppController;

public partial interface IAppController
{
    void Shutdown(); 
    bool TryRestart();
}
