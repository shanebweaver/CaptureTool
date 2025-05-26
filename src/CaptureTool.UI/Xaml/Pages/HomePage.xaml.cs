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
            imageAnimation?.TryStart(NewImageCaptureButton);

            ConnectedAnimation videoAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("backAnimation_Video");
            videoAnimation?.TryStart(NewVideoCaptureButton);
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (e.SourcePageType == PageLocator.GetPageType(CaptureToolNavigationRoutes.ImageCaptureOptions))
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("forwardAnimation_Image", NewImageCaptureButton);
        }
        else if (e.SourcePageType == PageLocator.GetPageType(CaptureToolNavigationRoutes.VideoCaptureOptions))
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("forwardAnimation_Video", NewVideoCaptureButton);
        }

        base.OnNavigatingFrom(e);
    }
}
