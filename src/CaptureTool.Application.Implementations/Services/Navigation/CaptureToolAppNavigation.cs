using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Navigation;
using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Application.Implementations.Services.Navigation;

public sealed partial class CaptureToolAppNavigation : IAppNavigation
{
    private readonly INavigationService _navigationService;
    public CaptureToolAppNavigation(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanGoBack => _navigationService.CanGoBack;

    public bool TryGoBack() => CanGoBack && _navigationService.TryGoBack();

    public void GoBackToMainWindow()
    {
        bool success = _navigationService.TryGoBackTo(r => CaptureToolNavigationRouteHelper.IsMainWindowRoute(r.Route));
        if (!success)
        {
            GoHome();
        }
    }

    public void GoBackOrGoHome()
    {
        if (!TryGoBack())
        {
            GoHome();
        }
    }

    public void GoHome()
        => _navigationService.Navigate(CaptureToolNavigationRoute.Home, clearHistory: true);

    public void GoToLoading()
        => _navigationService.Navigate(CaptureToolNavigationRoute.Loading, clearHistory: true);

    public void GoToError(Exception exception)
        => _navigationService.Navigate(CaptureToolNavigationRoute.Error, exception, true);

    public void GoToSettings()
        => _navigationService.Navigate(CaptureToolNavigationRoute.Settings);

    public void GoToAbout()
        => _navigationService.Navigate(CaptureToolNavigationRoute.About);

    public void GoToAddOns()
        => _navigationService.Navigate(CaptureToolNavigationRoute.AddOns);

    public void GoToImageCapture(CaptureOptions captureOptions, bool clearHistory = false)
        => _navigationService.Navigate(CaptureToolNavigationRoute.ImageCapture, captureOptions, clearHistory);

    public void GoToVideoCapture(NewCaptureArgs captureArgs)
        => _navigationService.Navigate(CaptureToolNavigationRoute.VideoCapture, captureArgs);

    public void GoToAudioCapture()
        => _navigationService.Navigate(CaptureToolNavigationRoute.AudioCapture, clearHistory: true);

    public void GoToAudioEdit()
        => _navigationService.Navigate(CaptureToolNavigationRoute.AudioEdit, clearHistory: true);

    public void GoToImageEdit(IImageFile imageFile)
        => _navigationService.Navigate(CaptureToolNavigationRoute.ImageEdit, imageFile, true);

    public void GoToVideoEdit(IVideoFile videoFile)
        => _navigationService.Navigate(CaptureToolNavigationRoute.VideoEdit, videoFile, true);
}
