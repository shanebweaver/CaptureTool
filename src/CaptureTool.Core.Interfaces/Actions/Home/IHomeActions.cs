namespace CaptureTool.Core.Interfaces.Actions.Home;

public interface IHomeActions
{
    bool CanNewImageCapture();
    bool CanNewVideoCapture();
    void NewImageCapture();
    void NewVideoCapture();
}
