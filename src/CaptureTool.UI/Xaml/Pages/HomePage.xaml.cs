using CaptureTool.Core;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class HomePage : HomePageBase
{
    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.NavigationMode == NavigationMode.Back)
        {
            ConnectedAnimation imageAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("backAnimation_Image");
            imageAnimation?.TryStart(NewDesktopImageCaptureButton);

            ConnectedAnimation videoAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("backAnimation_Video");
            videoAnimation?.TryStart(NewDesktopVideoCaptureButton);
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (e.SourcePageType == PageLocator.GetPageType(CaptureToolNavigationRoutes.DesktopImageCaptureOptions))
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("forwardAnimation_Image", NewDesktopImageCaptureButton);
        }
        else if (e.SourcePageType == PageLocator.GetPageType(CaptureToolNavigationRoutes.DesktopVideoCaptureOptions))
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("forwardAnimation_Video", NewDesktopVideoCaptureButton);
        }

        base.OnNavigatingFrom(e);
    }
}
