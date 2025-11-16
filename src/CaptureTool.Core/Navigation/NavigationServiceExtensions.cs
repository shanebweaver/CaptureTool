using CaptureTool.Capture;
using CaptureTool.Common.Storage;
using CaptureTool.Services.Navigation;
using System;

namespace CaptureTool.Core.Navigation;

public static partial class NavigationServiceExtensions
{
    public static void GoHome(this INavigationService navigationService)
        => navigationService.Navigate(NavigationRoute.Home, clearHistory: true);

    public static void GoToLoading(this INavigationService navigationService)
        => navigationService.Navigate(NavigationRoute.Loading, clearHistory: true);

    public static void GoToError(this INavigationService navigationService, Exception exception)
        => navigationService.Navigate(NavigationRoute.Error, exception, true);

    public static void GoToSettings(this INavigationService navigationService)
        => navigationService.Navigate(NavigationRoute.Settings);

    public static void GoToAbout(this INavigationService navigationService)
        => navigationService.Navigate(NavigationRoute.About);

    public static void GoToAddOns(this INavigationService navigationService)
        => navigationService.Navigate(NavigationRoute.AddOns);

    public static void GoToImageCapture(this INavigationService navigationService, CaptureOptions captureOptions, bool clearHistory = false)
        => navigationService.Navigate(NavigationRoute.ImageCapture, captureOptions, clearHistory);

    public static void GoToVideoCapture(this INavigationService navigationService, NewCaptureArgs captureargs)
        => navigationService.Navigate(NavigationRoute.VideoCapture, captureargs);

    public static void GoToImageEdit(this INavigationService navigationService, ImageFile imageFile)
        => navigationService.Navigate(NavigationRoute.ImageEdit, imageFile, true);

    public static void GoToVideoEdit(this INavigationService navigationService, VideoFile videoFile)
        => navigationService.Navigate(NavigationRoute.VideoEdit, videoFile, true);
}
