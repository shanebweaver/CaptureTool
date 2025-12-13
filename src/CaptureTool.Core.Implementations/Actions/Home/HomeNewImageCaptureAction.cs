using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Home;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Implementations.Actions.Home;

public sealed partial class HomeNewImageCaptureAction : ActionCommand, IHomeNewImageCaptureAction
{
    private readonly IAppNavigation _appNavigation;
    public HomeNewImageCaptureAction(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
    }
}
