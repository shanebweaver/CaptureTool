using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Application.Interfaces.Navigation;

public interface IAppNavigation
{
    bool CanGoBack { get; }

    void GoBackOrGoHome();
    void GoBackToMainWindow();
    void GoHome();
    void GoToAbout();
    void GoToAddOns();
    void GoToError(Exception exception);
    void GoToImageCapture(CaptureOptions captureOptions, bool clearHistory = false);
    void GoToImageEdit(IImageFile imageFile);
    void GoToLoading();
    void GoToSettings();
    void GoToVideoCapture(NewCaptureArgs captureargs);
    void GoToVideoEdit(IVideoFile videoFile);
    bool TryGoBack();
}