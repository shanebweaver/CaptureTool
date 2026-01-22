using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Home;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Implementations.Actions.Home;

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
