using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Navigation;

namespace CaptureTool.Core.Navigation;

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

    public void GoToImageEdit(ImageFile imageFile)
        => _navigationService.Navigate(CaptureToolNavigationRoute.ImageEdit, imageFile, true);

    public void GoToVideoEdit(VideoFile videoFile)
        => _navigationService.Navigate(CaptureToolNavigationRoute.VideoEdit, videoFile, true);
}
