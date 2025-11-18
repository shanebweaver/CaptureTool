using CaptureTool.Capture;
using CaptureTool.Common.Storage;
using CaptureTool.Services.Navigation;
using System;

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
        bool success = _navigationService.TryGoBackTo(r => CaptureToolNavigationRoutes.IsMainWindowRoute(r.Route));
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
        => _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);

    public void GoToLoading()
        => _navigationService.Navigate(NavigationRoute.Loading, clearHistory: true);

    public void GoToError(Exception exception)
        => _navigationService.Navigate(NavigationRoute.Error, exception, true);

    public void GoToSettings()
        => _navigationService.Navigate(NavigationRoute.Settings);

    public void GoToAbout()
        => _navigationService.Navigate(NavigationRoute.About);

    public void GoToAddOns()
        => _navigationService.Navigate(NavigationRoute.AddOns);

    public void GoToImageCapture(CaptureOptions captureOptions, bool clearHistory = false)
        => _navigationService.Navigate(NavigationRoute.ImageCapture, captureOptions, clearHistory);

    public void GoToVideoCapture(NewCaptureArgs captureargs)
        => _navigationService.Navigate(NavigationRoute.VideoCapture, captureargs);

    public void GoToImageEdit(ImageFile imageFile)
        => _navigationService.Navigate(NavigationRoute.ImageEdit, imageFile, true);

    public void GoToVideoEdit(VideoFile videoFile)
        => _navigationService.Navigate(NavigationRoute.VideoEdit, videoFile, true);
}
