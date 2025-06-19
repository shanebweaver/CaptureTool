using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class VideoCaptureOptionsPage : VideoCaptureOptionsPageBase
{
    public VideoCaptureOptionsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("forwardAnimation_Video");
        animation?.TryStart(NewVideoCaptureButton);
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (e.NavigationMode == NavigationMode.Back)
        {
            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("backAnimation_Video", NewVideoCaptureButton);
            animation.Configuration = new DirectConnectedAnimationConfiguration();
        }
        base.OnNavigatingFrom(e);
    }
}
