using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.UseCases.Home;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.Implementations.UseCases.Home;

public sealed partial class HomeNewVideoCaptureUseCase : UseCase, IHomeNewVideoCaptureUseCase
{
    private readonly IAppNavigation _appNavigation;

    public HomeNewVideoCaptureUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault);
    }
}
