using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class DesktopVideoCaptureOptionsPage : DesktopVideoCaptureOptionsPageBase
{
    public DesktopVideoCaptureOptionsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("forwardAnimation");
        animation?.TryStart(NewDesktopVideoCaptureButton);
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (e.NavigationMode == NavigationMode.Back)
        {
            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("backAnimation", NewDesktopVideoCaptureButton);
            animation.Configuration = new DirectConnectedAnimationConfiguration();
        }
        base.OnNavigatingFrom(e);
    }
}
