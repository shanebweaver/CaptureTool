using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class ImageCaptureOptionsPage : ImageCaptureOptionsPageBase
{
    public ImageCaptureOptionsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("forwardAnimation_Image");
        animation?.TryStart(NewImageCaptureButton);
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (e.NavigationMode == NavigationMode.Back)
        {
            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("backAnimation_Image", NewImageCaptureButton);
            animation.Configuration = new DirectConnectedAnimationConfiguration();
        }
        base.OnNavigatingFrom(e);
    }
}
