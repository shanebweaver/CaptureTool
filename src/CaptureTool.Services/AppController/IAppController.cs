namespace CaptureTool.Services.AppController;

public interface IAppController
{
    void Shutdown();

    bool TryRestart();
}
