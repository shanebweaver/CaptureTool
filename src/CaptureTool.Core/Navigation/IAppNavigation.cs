using CaptureTool.Capture;
using CaptureTool.Common.Storage;
using System;

namespace CaptureTool.Core.Navigation
{
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
        void GoToImageEdit(ImageFile imageFile);
        void GoToLoading();
        void GoToSettings();
        void GoToVideoCapture(NewCaptureArgs captureargs);
        void GoToVideoEdit(VideoFile videoFile);
        bool TryGoBack();
    }
}