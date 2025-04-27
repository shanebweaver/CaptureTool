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
            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("backAnimation");
            animation?.TryStart(NewDesktopImageCaptureButton);
            animation?.TryStart(NewDesktopVideoCaptureButton);
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (e.SourcePageType == PageLocator.GetPageType(NavigationRoutes.DesktopImageCaptureOptions))
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("forwardAnimation", NewDesktopImageCaptureButton);
        }
        else if (e.SourcePageType == PageLocator.GetPageType(NavigationRoutes.DesktopVideoCaptureOptions))
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("forwardAnimation", NewDesktopVideoCaptureButton);
        }

        base.OnNavigatingFrom(e);
    }
}
