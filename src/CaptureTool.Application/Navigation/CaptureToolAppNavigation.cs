using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Navigation;

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
        => _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);

    public void GoToLoading()
        => _navigationService.Navigate(NavigationRoute.Loading, clearHistory: true);

    public void GoToError(Exception exception)
        => _navigationService.Navigate(NavigationRoute.Error, exception, true);

    public void GoToSettings()
        => _navigationService.Navigate(NavigationRoute.Settings);

    public void GoToAbout()
        => _navigationService.Navigate(NavigationRoute.About);

    public void GoToStore()
        => _navigationService.Navigate(NavigationRoute.Store);

    public void GoToImageCapture(CaptureOptions captureOptions, bool clearHistory = false)
        => _navigationService.Navigate(NavigationRoute.ImageCapture, captureOptions, clearHistory);

    public void GoToVideoCapture(NewCaptureArgs captureArgs)
        => _navigationService.Navigate(NavigationRoute.VideoCapture, captureArgs);

    public void GoToAudioCapture()
        => _navigationService.Navigate(NavigationRoute.AudioCapture, clearHistory: true);

    public void GoToAudioEdit()
        => _navigationService.Navigate(NavigationRoute.AudioEdit, clearHistory: true);

    public void GoToAudioEdit(IAudioFile audioFile)
        => _navigationService.Navigate(NavigationRoute.AudioEdit, audioFile, true);

    public void GoToImageEdit(IImageFile imageFile)
        => _navigationService.Navigate(NavigationRoute.ImageEdit, imageFile, true);

    public void GoToVideoEdit(IVideoFile videoFile)
        => _navigationService.Navigate(NavigationRoute.VideoEdit, videoFile, true);
}
