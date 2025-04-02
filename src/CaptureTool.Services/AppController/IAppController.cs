namespace CaptureTool.Services.AppController;

public interface IAppController
{
    void Shutdown();
    bool TryRestart();

    void NewDesktopCapture();
    void NewVideoCapture();
    void NewAudioCapture();
}
